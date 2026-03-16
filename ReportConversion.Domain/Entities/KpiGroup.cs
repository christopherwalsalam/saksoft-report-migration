namespace ReportConversion.Domain.Entities;

public class KpiGroup
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>
    /// LLM-generated explanation of WHY the reports in this group are grouped together —
    /// e.g. common tables, shared aggregation patterns, identical calculation logic.
    /// </summary>
    public string? Reason { get; set; }

    public DateTime CreatedAt { get; set; }
    public ICollection<Report> Reports { get; set; } = new List<Report>();
    public ICollection<KpiRegistry> KpiRegistries { get; set; } = new List<KpiRegistry>();
    public ICollection<ReportKpiGroupMapping> ReportMappings { get; set; } = new List<ReportKpiGroupMapping>();
}
