namespace ReportConversion.Domain.Entities;

public class ReportDataSource
{
    public int Id { get; set; }
    public int ReportId { get; set; }
    public string DataSourceName { get; set; } = string.Empty;
    public string? ConnectionString { get; set; }
    public string? ServerName { get; set; }
    public string? DatabaseName { get; set; }
    public string? DataSourceType { get; set; }
    public Report? Report { get; set; }
}
