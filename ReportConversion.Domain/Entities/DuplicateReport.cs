using ReportConversion.Domain.Enums;

namespace ReportConversion.Domain.Entities;

public class DuplicateReport
{
    public int Id { get; set; }
    public int ReportId1 { get; set; }
    public int ReportId2 { get; set; }
    public double SimilarityScore { get; set; }
    public DuplicateAction RecommendedAction { get; set; }

    /// <summary>
    /// Records which detection tier identified this pair:
    ///   SqlFingerprint    — Tier 1: exact/partial SQL hash set match (most reliable)
    ///   SqlEmbedding      — Tier 2: semantic similarity on SQL text embeddings
    ///   MetadataEmbedding — Tier 3: semantic similarity on Name+Description (fallback)
    /// </summary>
    public DetectionMethod DetectionMethod { get; set; }

    public DateTime DetectedAt { get; set; }
    public Report? Report1 { get; set; }
    public Report? Report2 { get; set; }
}
