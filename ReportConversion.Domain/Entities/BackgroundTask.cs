namespace ReportConversion.Domain.Entities;

/// <summary>
/// Tracks the progress of a long-running background job triggered by the UI.
/// </summary>
public class BackgroundTask
{
    public int Id { get; set; }

    /// <summary>
    /// Unique string ID surfaced to the UI (matches or derives from Hangfire job ID).
    /// </summary>
    public string TaskId { get; set; } = string.Empty;

    /// <summary>
    /// Logical type: Extraction | StaleReport | KpiGrouping | DuplicateDetection | Migration
    /// </summary>
    public string TaskType { get; set; } = string.Empty;

    /// <summary>
    /// Pending | Running | Completed | Failed
    /// </summary>
    public string Status { get; set; } = "Pending";

    public int ProgressPercent { get; set; }

    public string? CurrentStep { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    public string? ErrorMessage { get; set; }
}
