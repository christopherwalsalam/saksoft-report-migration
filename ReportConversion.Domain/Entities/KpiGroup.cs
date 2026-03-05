namespace ReportConversion.Domain.Entities;

public class KpiGroup
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public ICollection<Report> Reports { get; set; } = new List<Report>();
    public ICollection<KpiRegistry> KpiRegistries { get; set; } = new List<KpiRegistry>();
}
