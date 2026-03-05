using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReportConversion.Application.Interfaces;
using ReportConversion.Application.Settings;
using ReportConversion.Domain.Entities;
using ReportConversion.Domain.Enums;
using System.Text.Json;

namespace ReportConversion.Application.Services;

public class DuplicateDetectionService
{
    private readonly IAzureOpenAiClient _openAiClient;
    private readonly IReportRepository _reportRepository;
    private readonly IDuplicateReportRepository _duplicateRepository;
    private readonly ILogger<DuplicateDetectionService> _logger;
    private readonly DuplicateDetectionSettings _settings;

    public DuplicateDetectionService(
        IAzureOpenAiClient openAiClient,
        IReportRepository reportRepository,
        IDuplicateReportRepository duplicateRepository,
        ILogger<DuplicateDetectionService> logger,
        IOptions<DuplicateDetectionSettings> settings)
    {
        _openAiClient = openAiClient;
        _reportRepository = reportRepository;
        _duplicateRepository = duplicateRepository;
        _logger = logger;
        _settings = settings.Value;
    }

    public async Task<DuplicateDetectionResult> FindDuplicatesAsync(CancellationToken cancellationToken = default)
    {
        var result = new DuplicateDetectionResult();
        var reports = (await _reportRepository.GetAllForEmbeddingAsync()).ToList();

        _logger.LogInformation("Generating embeddings for {Count} reports", reports.Count);

        // Generate embeddings for reports that don't have them
        foreach (var report in reports.Where(r => string.IsNullOrEmpty(r.EmbeddingVector)))
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
                _logger.LogWarning(ex, "Failed to generate embedding for report {Id}", report.Id);
            }
        }

        // Load all embeddings
        var vectors = new Dictionary<int, float[]>();
        foreach (var report in reports.Where(r => !string.IsNullOrEmpty(r.EmbeddingVector)))
        {
            try
            {
                var vec = JsonSerializer.Deserialize<float[]>(report.EmbeddingVector!);
                if (vec != null) vectors[report.Id] = vec;
            }
            catch { /* skip malformed */ }
        }

        _logger.LogInformation("Comparing {Count} embeddings for duplicates", vectors.Count);
        await _duplicateRepository.DeleteAllAsync();

        var ids = vectors.Keys.ToList();
        for (int i = 0; i < ids.Count; i++)
        {
            for (int j = i + 1; j < ids.Count; j++)
            {
                var similarity = CosineSimilarity(vectors[ids[i]], vectors[ids[j]]);
                if (similarity >= _settings.SimilarityThreshold)
                {
                    var duplicate = new DuplicateReport
                    {
                        ReportId1 = ids[i],
                        ReportId2 = ids[j],
                        SimilarityScore = similarity,
                        RecommendedAction = similarity >= 0.99 ? DuplicateAction.Archive : DuplicateAction.Review,
                        DetectedAt = DateTime.UtcNow
                    };
                    await _duplicateRepository.InsertAsync(duplicate);
                    result.DuplicatePairsFound++;
                }
            }
        }

        _logger.LogInformation("Duplicate detection complete. Found {Count} pairs", result.DuplicatePairsFound);
        return result;
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        double dot = 0, magA = 0, magB = 0;
        for (int i = 0; i < Math.Min(a.Length, b.Length); i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }
        return magA == 0 || magB == 0 ? 0 : dot / (Math.Sqrt(magA) * Math.Sqrt(magB));
    }
}

public class DuplicateDetectionResult
{
    public int DuplicatePairsFound { get; set; }
}
