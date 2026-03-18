namespace ReportConversion.Application.Models;

/// <summary>
/// Returned by GET /api/migration/summary after a migration task completes.
/// </summary>
public class MigrationSummaryDto
{
    public int TotalAttempted { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public double SuccessRate { get; set; }
    public string? LogDownloadUrl { get; set; }
    public IEnumerable<KpiBreakdownItem> KpiBreakdown { get; set; } = Enumerable.Empty<KpiBreakdownItem>();
}

public class KpiBreakdownItem
{
    public string KpiGroup { get; set; } = string.Empty;
    public int Attempted { get; set; }
    public int Succeeded { get; set; }
    public int Failed { get; set; }
}
