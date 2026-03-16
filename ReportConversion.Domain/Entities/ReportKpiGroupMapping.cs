namespace ReportConversion.Domain.Entities;

public class ReportKpiGroupMapping
{
    public int Id { get; set; }
    public int ReportId { get; set; }
    public int KpiGroupId { get; set; }
    public DateTime AssignedAt { get; set; }
    public Report? Report { get; set; }
    public KpiGroup? KpiGroup { get; set; }
}
