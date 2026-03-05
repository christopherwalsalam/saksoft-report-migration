using ReportConversion.Domain.Enums;

namespace ReportConversion.Domain.Entities;

public class MigrationJob
{
    public int Id { get; set; }
    public int ReportId { get; set; }
    public MigrationStatus Status { get; set; }
    public string? PowerBiReportId { get; set; }
    public string? OutputFilePath { get; set; }
    public string? BlobStoragePath { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? HangfireJobId { get; set; }
    public Report? Report { get; set; }
    public ICollection<MigrationLog> Logs { get; set; } = new List<MigrationLog>();
}
