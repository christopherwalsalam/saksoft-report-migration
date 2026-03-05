namespace ReportConversion.Domain.Entities;

public class MigrationLog
{
    public int Id { get; set; }
    public int MigrationJobId { get; set; }
    public int ReportId { get; set; }
    public string LogLevel { get; set; } = "Info";
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
    public DateTime Timestamp { get; set; }
    public MigrationJob? MigrationJob { get; set; }
}
