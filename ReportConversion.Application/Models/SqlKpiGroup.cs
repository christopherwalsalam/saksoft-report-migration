namespace ReportConversion.Application.Models;

/// <summary>
/// Represents a KPI group discovered by the LLM from SQL technical analysis.
/// The group name and description are derived entirely from SQL patterns —
/// not from predefined categories.
/// </summary>
public class SqlKpiGroup
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// LLM-generated explanation of WHY these specific reports are grouped together —
    /// citing the concrete SQL evidence (shared tables, identical aggregation columns,
    /// common join patterns, matching filter fields, etc.).
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    public List<int> ReportIds { get; set; } = new();
}

/// <summary>
/// Result returned by the SQL-based KPI grouping operation.
/// </summary>
public class SqlKpiGroupingResult
{
    public int GroupsDiscovered { get; set; }
    public int ReportsGrouped { get; set; }
    public int ReportsWithoutSql { get; set; }
    public int FailedCount { get; set; }
}
