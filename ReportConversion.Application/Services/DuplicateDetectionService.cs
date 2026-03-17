using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReportConversion.Application.Interfaces;
using ReportConversion.Application.Settings;
using ReportConversion.Domain.Entities;
using ReportConversion.Domain.Enums;
using System.Text.Json;

namespace ReportConversion.Application.Services;

/// <summary>
/// Detects duplicate reports using a three-tier strategy:
///
///  Tier 1 — SQL Fingerprint (Jaccard similarity on MD5 hash sets)
///    Applied when BOTH reports have SQL.  Deterministic — no LLM cost.
///    Jaccard = 1.0  → exact duplicate  → Archive
///    Jaccard ≥ SqlFingerprintJaccardThreshold → partial overlap → Review
///
///  Tier 2 — SQL Semantic Embedding (cosine similarity on SQL text embeddings)
///    Applied when both reports have SQL but were not flagged by Tier 1.
///    Catches structurally similar queries with different aliases or column order.
///
///  Tier 3 — Metadata Embedding (cosine similarity on Name + Description)
///    Fallback for reports that have NO SQL at all.
///    Never used for reports that have SQL — SQL evidence always takes priority.
/// </summary>
public class DuplicateDetectionService
{
    private readonly IAzureOpenAiClient _openAiClient;
    private readonly IReportRepository _reportRepository;
    private readonly IReportSqlRepository _sqlRepository;
    private readonly IDuplicateReportRepository _duplicateRepository;
    private readonly ILogger<DuplicateDetectionService> _logger;
    private readonly DuplicateDetectionSettings _settings;

    public DuplicateDetectionService(
        IAzureOpenAiClient openAiClient,
        IReportRepository reportRepository,
        IReportSqlRepository sqlRepository,
        IDuplicateReportRepository duplicateRepository,
        ILogger<DuplicateDetectionService> logger,
        IOptions<DuplicateDetectionSettings> settings)
    {
        _openAiClient = openAiClient;
        _reportRepository = reportRepository;
        _sqlRepository = sqlRepository;
        _duplicateRepository = duplicateRepository;
        _logger = logger;
        _settings = settings.Value;
    }

    public async Task<DuplicateDetectionResult> FindDuplicatesAsync(CancellationToken cancellationToken = default)
    {
        var result = new DuplicateDetectionResult();
        await _duplicateRepository.DeleteAllAsync();

        // Load all SQL records in one query, grouped by report
        var allSql = (await _sqlRepository.GetAllAsync()).ToList();
        var sqlByReport = allSql
            .GroupBy(s => s.ReportId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var allReports       = (await _reportRepository.GetAllForEmbeddingAsync()).ToList();
        var reportsWithSql   = sqlByReport.Keys.ToList();
        var reportsWithoutSql = allReports
            .Select(r => r.Id)
            .Except(reportsWithSql)
            .ToList();

        _logger.LogInformation(
            "Duplicate detection starting. Reports with SQL: {WithSql}, without SQL: {WithoutSql}",
            reportsWithSql.Count, reportsWithoutSql.Count);

        // Track pairs already recorded so Tier 2 doesn't re-examine Tier 1 hits
        var processedPairs = new HashSet<(int, int)>();

        // ── Tier 1: SQL Fingerprint (Jaccard) ────────────────────────────────
        _logger.LogInformation("Tier 1: SQL fingerprint comparison");

        for (int i = 0; i < reportsWithSql.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested) break;
            for (int j = i + 1; j < reportsWithSql.Count; j++)
            {
                if (cancellationToken.IsCancellationRequested) break;

                var idA = reportsWithSql[i];
                var idB = reportsWithSql[j];

                var fpA = sqlByReport[idA]
                    .Select(s => s.SqlFingerprint)
                    .Where(f => !string.IsNullOrEmpty(f))
                    .ToHashSet();

                var fpB = sqlByReport[idB]
                    .Select(s => s.SqlFingerprint)
                    .Where(f => !string.IsNullOrEmpty(f))
                    .ToHashSet();

                // Skip if neither side has computed fingerprints
                if (fpA.Count == 0 || fpB.Count == 0) continue;

                var intersection = fpA.Intersect(fpB).Count();
                var union        = fpA.Union(fpB).Count();
                var jaccard      = (double)intersection / union;

                if (jaccard >= _settings.SqlFingerprintJaccardThreshold)
                {
                    // Jaccard == 1.0 means every query in both reports is identical → Archive
                    var action = jaccard >= 1.0 ? DuplicateAction.Archive : DuplicateAction.Review;

                    await _duplicateRepository.InsertAsync(new DuplicateReport
                    {
                        ReportId1         = idA,
                        ReportId2         = idB,
                        SimilarityScore   = jaccard,
                        RecommendedAction = action,
                        DetectionMethod   = DetectionMethod.SqlFingerprint,
                        DetectedAt        = DateTime.UtcNow
                    });

                    result.SqlFingerprintPairs++;
                    processedPairs.Add((idA, idB));
                }
            }
        }

        _logger.LogInformation("Tier 1 complete. Pairs found: {Count}", result.SqlFingerprintPairs);

        // ── Tier 2: SQL Semantic Embedding ────────────────────────────────────
        // Generate embeddings from the full SQL text of each report
        _logger.LogInformation("Tier 2: SQL embedding comparison");

        var sqlEmbeddings = new Dictionary<int, float[]>();
        foreach (var reportId in reportsWithSql)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var combinedSql = string.Join("\n\n",
                sqlByReport[reportId]
                    .Select(s => s.SqlText)
                    .Where(t => !string.IsNullOrWhiteSpace(t)));

            if (string.IsNullOrWhiteSpace(combinedSql)) continue;

            try
            {
                var embedding = await _openAiClient.GetEmbeddingAsync(combinedSql);
                sqlEmbeddings[reportId] = embedding.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed SQL embedding for report {Id}", reportId);
            }
        }

        var sqlIds = sqlEmbeddings.Keys.ToList();
        for (int i = 0; i < sqlIds.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested) break;
            for (int j = i + 1; j < sqlIds.Count; j++)
            {
                if (cancellationToken.IsCancellationRequested) break;

                var idA = sqlIds[i];
                var idB = sqlIds[j];

                // Skip pairs already recorded in Tier 1
                if (processedPairs.Contains((idA, idB))) continue;

                var similarity = CosineSimilarity(sqlEmbeddings[idA], sqlEmbeddings[idB]);
                if (similarity >= _settings.SqlEmbeddingThreshold)
                {
                    await _duplicateRepository.InsertAsync(new DuplicateReport
                    {
                        ReportId1         = idA,
                        ReportId2         = idB,
                        SimilarityScore   = similarity,
                        RecommendedAction = DuplicateAction.Review,
                        DetectionMethod   = DetectionMethod.SqlEmbedding,
                        DetectedAt        = DateTime.UtcNow
                    });

                    result.SqlEmbeddingPairs++;
                    processedPairs.Add((idA, idB));
                }
            }
        }

        _logger.LogInformation("Tier 2 complete. Pairs found: {Count}", result.SqlEmbeddingPairs);

        // ── Tier 3: Metadata Embedding (no SQL available) ─────────────────────
        // Only compare reports that have no SQL against other no-SQL reports
        _logger.LogInformation("Tier 3: Metadata embedding for {Count} reports without SQL", reportsWithoutSql.Count);

        var reportsForMeta = allReports
            .Where(r => reportsWithoutSql.Contains(r.Id))
            .ToList();

        // Generate embeddings for reports that don't have a cached one
        foreach (var report in reportsForMeta.Where(r => string.IsNullOrEmpty(r.EmbeddingVector)))
        {
            if (cancellationToken.IsCancellationRequested) break;
            try
            {
                var text = $"{report.Name} {report.Description}";
                var embedding = await _openAiClient.GetEmbeddingAsync(text);
                var json = JsonSerializer.Serialize(embedding);
                await _reportRepository.UpdateEmbeddingAsync(report.Id, json);
                report.EmbeddingVector = json;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed metadata embedding for report {Id}", report.Id);
            }
        }

        var metaVectors = new Dictionary<int, float[]>();
        foreach (var report in reportsForMeta.Where(r => !string.IsNullOrEmpty(r.EmbeddingVector)))
        {
            try
            {
                var vec = JsonSerializer.Deserialize<float[]>(report.EmbeddingVector!);
                if (vec != null) metaVectors[report.Id] = vec;
            }
            catch { /* skip malformed vectors */ }
        }

        var metaIds = metaVectors.Keys.ToList();
        for (int i = 0; i < metaIds.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested) break;
            for (int j = i + 1; j < metaIds.Count; j++)
            {
                if (cancellationToken.IsCancellationRequested) break;

                var idA = metaIds[i];
                var idB = metaIds[j];

                var similarity = CosineSimilarity(metaVectors[idA], metaVectors[idB]);
                if (similarity >= _settings.MetadataEmbeddingThreshold)
                {
                    await _duplicateRepository.InsertAsync(new DuplicateReport
                    {
                        ReportId1         = idA,
                        ReportId2         = idB,
                        SimilarityScore   = similarity,
                        RecommendedAction = DuplicateAction.Review,
                        DetectionMethod   = DetectionMethod.MetadataEmbedding,
                        DetectedAt        = DateTime.UtcNow
                    });

                    result.MetadataEmbeddingPairs++;
                }
            }
        }

        _logger.LogInformation("Tier 3 complete. Pairs found: {Count}", result.MetadataEmbeddingPairs);

        result.TotalPairsFound = result.SqlFingerprintPairs + result.SqlEmbeddingPairs + result.MetadataEmbeddingPairs;
        _logger.LogInformation(
            "Duplicate detection complete — SQL Fingerprint: {F}, SQL Embedding: {S}, Metadata Embedding: {M}, Total: {T}",
            result.SqlFingerprintPairs, result.SqlEmbeddingPairs, result.MetadataEmbeddingPairs, result.TotalPairsFound);

        return result;
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        double dot = 0, magA = 0, magB = 0;
        for (int k = 0; k < Math.Min(a.Length, b.Length); k++)
        {
            dot  += a[k] * b[k];
            magA += a[k] * a[k];
            magB += b[k] * b[k];
        }
        return magA == 0 || magB == 0 ? 0 : dot / (Math.Sqrt(magA) * Math.Sqrt(magB));
    }
}

public class DuplicateDetectionResult
{
    public int SqlFingerprintPairs    { get; set; }
    public int SqlEmbeddingPairs      { get; set; }
    public int MetadataEmbeddingPairs { get; set; }
    public int TotalPairsFound        { get; set; }

    // Legacy property — kept so existing callers (MenuRunner, API) don't break
    public int DuplicatePairsFound => TotalPairsFound;
}
