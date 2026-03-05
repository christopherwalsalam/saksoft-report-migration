namespace ReportConversion.Domain.Entities;

public class ReportSql
{
    public int Id { get; set; }
    public int ReportId { get; set; }
    public string? SqlText { get; set; }
    public string? SqlFingerprint { get; set; }
    public string? QueryName { get; set; }
    public Report? Report { get; set; }
}
