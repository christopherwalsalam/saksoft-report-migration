using ReportConversion.Domain.Enums;

namespace ReportConversion.Domain.Entities;

public class DuplicateReport
{
    public int Id { get; set; }
    public int ReportId1 { get; set; }
    public int ReportId2 { get; set; }
    public double SimilarityScore { get; set; }
    public DuplicateAction RecommendedAction { get; set; }
    public DateTime DetectedAt { get; set; }
    public Report? Report1 { get; set; }
    public Report? Report2 { get; set; }
}
