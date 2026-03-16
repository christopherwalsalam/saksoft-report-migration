using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using ReportConversion.Application.Interfaces;
using ReportConversion.Application.Models;
using ReportConversion.Domain.Entities;

namespace ReportConversion.Application.Services;

/// <summary>
/// Groups reports by KPI using SQL technical analysis.
///
/// Instead of classifying reports into predefined categories, this service:
///  1. Loads every report's SQL queries from the database.
///  2. Extracts technical signals from each query: tables referenced, columns used,
///     aggregation functions, join patterns and filter patterns.
///  3. Sends all SQL summaries to Azure OpenAI (GPT-4o) which discovers emergent KPI
///     groups solely from the SQL logic — with no predefined categories.
///  4. Persists the discovered groups in KpiGroups and links each report via KpiGroupId.
///  5. Stores the per-report SQL analysis in the new ReportSqlAnalysis table so that
///     the technical basis for each grouping decision is auditable.
/// </summary>
public class KpiGroupingService
{
    private readonly IAzureOpenAiClient _openAiClient;
    private readonly IReportRepository _reportRepository;
    private readonly IReportSqlRepository _sqlRepository;
    private readonly IKpiGroupRepository _kpiGroupRepository;
    private readonly IReportSqlAnalysisRepository _sqlAnalysisRepository;
    private readonly ILogger<KpiGroupingService> _logger;

    // Maximum reports sent to the LLM in a single grouping call.
    // GPT-4o has a 128 K token window; each SQL summary is capped at ~400 chars,
    // so 100 reports ≈ 40 K chars which is comfortably within budget.
    private const int LlmBatchSize = 100;

    public KpiGroupingService(
        IAzureOpenAiClient openAiClient,
        IReportRepository reportRepository,
        IReportSqlRepository sqlRepository,
        IKpiGroupRepository kpiGroupRepository,
        IReportSqlAnalysisRepository sqlAnalysisRepository,
        ILogger<KpiGroupingService> logger)
    {
        _openAiClient = openAiClient;
        _reportRepository = reportRepository;
        _sqlRepository = sqlRepository;
        _kpiGroupRepository = kpiGroupRepository;
        _sqlAnalysisRepository = sqlAnalysisRepository;
        _logger = logger;
    }

    public async Task<SqlKpiGroupingResult> GroupReportsByKpiAsync(CancellationToken cancellationToken = default)
    {
        var result = new SqlKpiGroupingResult();
        var reports = (await _reportRepository.GetAllAsync(1, int.MaxValue)).ToList();

        _logger.LogInformation("SQL-based KPI grouping started for {Count} reports", reports.Count);

        // ── Phase 1: Extract SQL analysis for every report ───────────────────
        var sqlSummaries = new List<(int ReportId, string SqlSummary)>();

        foreach (var report in reports)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var sqls = (await _sqlRepository.GetByReportIdAsync(report.Id)).ToList();
            if (!sqls.Any() || sqls.All(s => string.IsNullOrWhiteSpace(s.SqlText)))
            {
                _logger.LogDebug("Report {Id} has no SQL — skipping grouping", report.Id);
                result.ReportsWithoutSql++;
                continue;
            }

            var analysis = ExtractSqlAnalysis(report.Id, sqls);

            // Persist the analysis for auditability
            await _sqlAnalysisRepository.UpsertAsync(analysis);

            sqlSummaries.Add((report.Id, analysis.SqlSummary!));
        }

        _logger.LogInformation("SQL analysis extracted for {Count} reports", sqlSummaries.Count);

        // ── Phase 2: LLM discovers emergent KPI groups from SQL patterns ──────
        // Process in batches so we stay within token limits for very large repos.
        var allDiscoveredGroups = new List<SqlKpiGroup>();

        for (int i = 0; i < sqlSummaries.Count; i += LlmBatchSize)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var batch = sqlSummaries.Skip(i).Take(LlmBatchSize).ToList();

            try
            {
                var groups = await _openAiClient.DiscoverKpiGroupsFromSqlAsync(batch);
                allDiscoveredGroups.AddRange(groups);
                _logger.LogInformation(
                    "LLM grouping batch {Batch}: discovered {Groups} groups covering {Reports} reports",
                    i / LlmBatchSize + 1, groups.Count, groups.Sum(g => g.ReportIds.Count));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LLM grouping failed for batch starting at index {Index}", i);
                result.FailedCount += batch.Count;
            }
        }

        // ── Phase 3: Merge groups with the same name across batches ──────────
        var mergedGroups = MergeGroups(allDiscoveredGroups);

        // ── Phase 4: Persist groups and assign reports ────────────────────────
        foreach (var group in mergedGroups)
        {
            if (cancellationToken.IsCancellationRequested) break;

            // Reuse existing group if the LLM produced the same name in a prior run
            var existing = await _kpiGroupRepository.GetByNameAsync(group.Name);
            int groupId;
            if (existing != null)
            {
                groupId = existing.Id;
            }
            else
            {
                groupId = await _kpiGroupRepository.InsertAsync(new KpiGroup
                {
                    Name = group.Name,
                    Description = group.Description,
                    CreatedAt = DateTime.UtcNow
                });
                result.GroupsDiscovered++;
            }

            foreach (var reportId in group.ReportIds)
            {
                try
                {
                    await _reportRepository.UpdateKpiGroupAsync(reportId, groupId);
                    result.ReportsGrouped++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to assign report {ReportId} to group '{Group}'", reportId, group.Name);
                    result.FailedCount++;
                }
            }
        }

        _logger.LogInformation(
            "SQL-based KPI grouping complete. Groups: {Groups}, Grouped: {Grouped}, WithoutSql: {NoSql}, Failed: {Failed}",
            result.GroupsDiscovered, result.ReportsGrouped, result.ReportsWithoutSql, result.FailedCount);

        return result;
    }

    // ── SQL Analysis Extraction ───────────────────────────────────────────────

    /// <summary>
    /// Extracts technical SQL signals from a report's queries and builds a compact
    /// summary string that can be sent to the LLM for grouping.
    /// </summary>
    private static ReportSqlAnalysis ExtractSqlAnalysis(int reportId, List<ReportSql> sqls)
    {
        var allSql = string.Join("\n\n", sqls
            .Where(s => !string.IsNullOrWhiteSpace(s.SqlText))
            .Select(s => s.SqlText!));

        var upper = allSql.ToUpperInvariant();

        var tables = ExtractTables(upper);
        var columns = ExtractSelectColumns(allSql);
        var aggregations = ExtractAggregations(upper);
        var joins = ExtractJoins(upper);
        var filters = ExtractFilterKeywords(upper);

        var summary = BuildSqlSummary(tables, columns, aggregations, joins, filters);

        return new ReportSqlAnalysis
        {
            ReportId = reportId,
            TablesReferenced = string.Join(", ", tables),
            ColumnsUsed = string.Join(", ", columns),
            AggregationPatterns = string.Join(", ", aggregations),
            JoinPatterns = string.Join(", ", joins),
            FilterPatterns = string.Join(", ", filters),
            SqlSummary = summary,
            AnalysedAt = DateTime.UtcNow
        };
    }

    private static IEnumerable<string> ExtractTables(string upperSql)
    {
        // Match identifiers after FROM or JOIN, optionally schema-qualified (schema.table)
        var matches = Regex.Matches(upperSql,
            @"(?:FROM|JOIN)\s+((?:\w+\.)?(\w+))(?:\s+(?:AS\s+)?\w+)?",
            RegexOptions.IgnoreCase);

        return matches
            .Select(m => m.Groups[1].Value.Trim())
            .Where(t => t.Length > 1 && !IsReservedWord(t))
            .Distinct()
            .OrderBy(t => t)
            .Take(20);
    }

    private static IEnumerable<string> ExtractSelectColumns(string sql)
    {
        // Pull out the SELECT clause (up to the first FROM) and extract identifiers
        var selectMatch = Regex.Match(sql, @"SELECT\s+(.*?)\s+FROM", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!selectMatch.Success) return Enumerable.Empty<string>();

        var selectClause = selectMatch.Groups[1].Value;

        // Strip function wrappers, keep identifiers longer than 2 chars
        var identifiers = Regex.Matches(selectClause, @"\b([A-Za-z_]\w+)\b")
            .Select(m => m.Groups[1].Value.ToUpperInvariant())
            .Where(c => c.Length > 2 && !IsReservedWord(c) && !IsAggregateFunction(c))
            .Distinct()
            .Take(15);

        return identifiers;
    }

    private static IEnumerable<string> ExtractAggregations(string upperSql)
    {
        var funcs = new[] { "SUM", "COUNT", "AVG", "MAX", "MIN", "STDEV", "VARIANCE" };
        var found = funcs.Where(f => Regex.IsMatch(upperSql, $@"\b{f}\s*\(")).ToList();

        if (Regex.IsMatch(upperSql, @"\bGROUP\s+BY\b")) found.Add("GROUP BY");
        if (Regex.IsMatch(upperSql, @"\bHAVING\b")) found.Add("HAVING");
        if (Regex.IsMatch(upperSql, @"\bORDER\s+BY\b")) found.Add("ORDER BY");
        if (Regex.IsMatch(upperSql, @"\bPARTITION\s+BY\b")) found.Add("PARTITION BY");

        return found.Distinct();
    }

    private static IEnumerable<string> ExtractJoins(string upperSql)
    {
        var joinTypes = new[] { "INNER JOIN", "LEFT JOIN", "RIGHT JOIN", "FULL OUTER JOIN", "CROSS JOIN" };
        return joinTypes.Where(j => upperSql.Contains(j)).Distinct();
    }

    private static IEnumerable<string> ExtractFilterKeywords(string upperSql)
    {
        var keywords = new List<string>();
        var whereMatch = Regex.Match(upperSql, @"\bWHERE\b(.+?)(?:\bGROUP\s+BY\b|\bORDER\s+BY\b|\bHAVING\b|$)",
            RegexOptions.Singleline);

        if (whereMatch.Success)
        {
            var whereClause = whereMatch.Groups[1].Value;
            var identifiers = Regex.Matches(whereClause, @"\b([A-Z_]\w+)\b")
                .Select(m => m.Value)
                .Where(w => w.Length > 3 && !IsReservedWord(w))
                .Distinct()
                .Take(10);
            keywords.AddRange(identifiers);
        }

        if (Regex.IsMatch(upperSql, @"\bBETWEEN\b")) keywords.Add("BETWEEN");
        if (Regex.IsMatch(upperSql, @"\bIN\s*\(")) keywords.Add("IN");
        if (Regex.IsMatch(upperSql, @"\bLIKE\b")) keywords.Add("LIKE");
        if (Regex.IsMatch(upperSql, @"\bDATEDIFF\b|\bDATEADD\b|\bYEAR\s*\(|\bMONTH\s*\(")) keywords.Add("DATE_FILTER");

        return keywords.Distinct().Take(15);
    }

    private static string BuildSqlSummary(
        IEnumerable<string> tables,
        IEnumerable<string> columns,
        IEnumerable<string> aggregations,
        IEnumerable<string> joins,
        IEnumerable<string> filters)
    {
        var sb = new StringBuilder();
        var tList = tables.ToList();
        var cList = columns.ToList();
        var aList = aggregations.ToList();
        var jList = joins.ToList();
        var fList = filters.ToList();

        if (tList.Any())  sb.AppendLine($"Tables: {string.Join(", ", tList)}");
        if (cList.Any())  sb.AppendLine($"Columns: {string.Join(", ", cList)}");
        if (aList.Any())  sb.AppendLine($"Aggregations: {string.Join(", ", aList)}");
        if (jList.Any())  sb.AppendLine($"Joins: {string.Join(", ", jList)}");
        if (fList.Any())  sb.AppendLine($"Filters: {string.Join(", ", fList)}");

        // Cap at 500 chars to keep LLM prompts manageable
        var summary = sb.ToString().Trim();
        return summary.Length > 500 ? summary[..500] : summary;
    }

    // ── Group Merging ─────────────────────────────────────────────────────────

    /// <summary>
    /// When processing in batches, the LLM may produce groups with the same name
    /// in different batches. This merges them into a single group.
    /// </summary>
    private static List<SqlKpiGroup> MergeGroups(List<SqlKpiGroup> groups)
    {
        var merged = new Dictionary<string, SqlKpiGroup>(StringComparer.OrdinalIgnoreCase);
        foreach (var g in groups)
        {
            if (merged.TryGetValue(g.Name, out var existing))
            {
                existing.ReportIds.AddRange(g.ReportIds.Except(existing.ReportIds));
            }
            else
            {
                merged[g.Name] = new SqlKpiGroup
                {
                    Name = g.Name,
                    Description = g.Description,
                    ReportIds = new List<int>(g.ReportIds)
                };
            }
        }
        return merged.Values.ToList();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool IsReservedWord(string word) =>
        _reservedWords.Contains(word.ToUpperInvariant());

    private static bool IsAggregateFunction(string word) =>
        _aggregateFunctions.Contains(word.ToUpperInvariant());

    private static readonly HashSet<string> _reservedWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "SELECT", "FROM", "WHERE", "JOIN", "INNER", "LEFT", "RIGHT", "OUTER", "FULL",
        "ON", "AND", "OR", "NOT", "IN", "IS", "NULL", "AS", "BY", "GROUP", "ORDER",
        "HAVING", "UNION", "ALL", "DISTINCT", "TOP", "WITH", "CASE", "WHEN", "THEN",
        "ELSE", "END", "INTO", "VALUES", "SET", "UPDATE", "DELETE", "INSERT",
        "CREATE", "TABLE", "INDEX", "VIEW", "PROCEDURE", "FUNCTION",
        "INT", "NVARCHAR", "VARCHAR", "DATETIME", "BIT", "FLOAT", "DECIMAL",
        "CAST", "CONVERT", "ISNULL", "COALESCE", "DATEADD", "DATEDIFF",
        "YEAR", "MONTH", "DAY", "GETDATE", "GETUTCDATE"
    };

    private static readonly HashSet<string> _aggregateFunctions = new(StringComparer.OrdinalIgnoreCase)
    {
        "SUM", "COUNT", "AVG", "MAX", "MIN", "STDEV", "VARIANCE", "COUNT_BIG"
    };
}
