using ReportConversion.Domain.Enums;

namespace ReportConversion.Domain.Entities;

public class Report
{
    public int Id { get; set; }
    public string SapReportId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Owner { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime LastModifiedDate { get; set; }
    public DateTime? LastRunDate { get; set; }
    public ReportType ReportType { get; set; }
    public string? FolderPath { get; set; }
    public string? Category { get; set; }
    public bool IsScheduled { get; set; }
    public bool HasActiveSubscriptions { get; set; }
    public UsageStatus UsageStatus { get; set; }
    public string? KpiGroupName { get; set; }
    public int? KpiGroupId { get; set; }
    public string? EmbeddingVector { get; set; }
    public DateTime ExtractedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<ReportElement> Elements { get; set; } = new List<ReportElement>();
    public ICollection<ReportSql> SqlQueries { get; set; } = new List<ReportSql>();
    public ICollection<ReportDataSource> DataSources { get; set; } = new List<ReportDataSource>();
}
