namespace ReportConversion.Application.Settings;

public class DuplicateDetectionSettings
{
    // ── Tier 1: SQL Fingerprint (Jaccard on MD5 hash sets) ───────────────────
    // Jaccard = 1.0  → exact SQL match  → Archive
    // Jaccard >= this threshold → partial SQL overlap → Review
    public double SqlFingerprintJaccardThreshold { get; set; } = 0.80;

    // ── Tier 2: SQL Semantic Embedding ───────────────────────────────────────
    // Cosine similarity on embeddings generated from concatenated SQL text.
    // Applied to reports that both have SQL but were not caught by Tier 1.
    public double SqlEmbeddingThreshold { get; set; } = 0.90;

    // ── Tier 3: Metadata Embedding (no SQL available) ────────────────────────
    // Cosine similarity on embeddings generated from report Name + Description.
    // Applied only to reports that have no SQL at all.
    public double MetadataEmbeddingThreshold { get; set; } = 0.92;

    // Legacy — kept so existing config files don't break.
    // Maps to MetadataEmbeddingThreshold when no explicit tier settings are supplied.
    public double SimilarityThreshold
    {
        get => MetadataEmbeddingThreshold;
        set => MetadataEmbeddingThreshold = value;
    }
}
