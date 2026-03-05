namespace ReportConversion.Domain.Entities;

public class KpiRegistry
{
    public int Id { get; set; }
    public string KpiName { get; set; } = string.Empty;
    public int KpiGroupId { get; set; }
    public string? BusinessDomain { get; set; }
    public string? MetricsInvolved { get; set; }
    public int ReportCount { get; set; }
    public string? DataSourcesInvolved { get; set; }
    public DateTime CreatedAt { get; set; }
    public KpiGroup? KpiGroupEntity { get; set; }
}
