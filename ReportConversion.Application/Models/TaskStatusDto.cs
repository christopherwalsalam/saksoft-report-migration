namespace ReportConversion.Application.Models;

/// <summary>
/// DTO returned by GET /api/tasks/{taskId}/status — consumed by the React UI polling loop.
/// </summary>
public class TaskStatusDto
{
    public string TaskId { get; set; } = string.Empty;
    public string TaskType { get; set; } = string.Empty;

    /// <summary>Pending | Running | Completed | Failed</summary>
    public string Status { get; set; } = "Pending";

    public int ProgressPercent { get; set; }
    public string? CurrentStep { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
}
