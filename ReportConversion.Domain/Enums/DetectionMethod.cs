namespace ReportConversion.Domain.Enums;

public enum DetectionMethod
{
    /// <summary>
    /// Tier 1: Jaccard similarity on MD5 SQL fingerprint sets.
    /// Deterministic — no LLM cost.  Score = 1.0 means byte-for-byte identical queries.
    /// </summary>
    SqlFingerprint,

    /// <summary>
    /// Tier 2: Cosine similarity on Azure OpenAI embeddings generated from the
    /// concatenated SQL text.  Used when both reports have SQL but fingerprints
    /// diverge below the exact-match threshold (different aliases, ordering, etc.).
    /// </summary>
    SqlEmbedding,

    /// <summary>
    /// Tier 3: Cosine similarity on Azure OpenAI embeddings generated from
    /// report Name + Description.  Fallback for reports that have no SQL at all.
    /// </summary>
    MetadataEmbedding
}
