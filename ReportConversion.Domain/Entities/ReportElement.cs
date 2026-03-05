namespace ReportConversion.Domain.Entities;

public class ReportElement
{
    public int Id { get; set; }
    public int ReportId { get; set; }
    public string ElementType { get; set; } = string.Empty; // Table, Chart, CrossTab, Image, Graph
    public string? ElementName { get; set; }
    public string? Properties { get; set; } // JSON
    public string? PowerBiEquivalent { get; set; }
    public Report? Report { get; set; }
}
