using ReportConversion.Application.Models;

namespace ReportConversion.Application.Interfaces;

public interface IAzureOpenAiClient
{
    Task<string> GetKpiGroupAsync(string reportMetadata);
    Task<List<float>> GetEmbeddingAsync(string text);
    Task<Dictionary<string, string>> GetKpiGroupsBatchAsync(List<(int Id, string Metadata)> reports);

    /// <summary>
    /// Analyses SQL summaries for a set of reports and discovers emergent KPI groups
    /// based purely on the technical SQL patterns (tables, columns, aggregations, joins).
    /// Returns a list of discovered groups, each with a name, description, and the
    /// report IDs that belong to it.
    /// </summary>
    Task<List<SqlKpiGroup>> DiscoverKpiGroupsFromSqlAsync(List<(int ReportId, string SqlSummary)> reports);
}
