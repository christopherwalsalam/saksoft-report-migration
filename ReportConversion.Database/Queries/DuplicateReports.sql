-- ============================================================
-- Duplicate Report Analysis Queries
-- Run each section independently in SSMS / Azure Data Studio.
-- ============================================================

USE ReportConversionDB;
GO

-- ============================================================
-- QUERY 1: FULL DUPLICATE REPORT LIST
-- One row per duplicate pair.
-- Shows what to do, why they are duplicates, how similar they
-- are, and key details of both reports side-by-side.
-- ============================================================
SELECT
    -- ── Pair identity ────────────────────────────────────────
    ROW_NUMBER() OVER (ORDER BY d.SimilarityScore DESC) AS PairNo,

    -- ── How similar? ─────────────────────────────────────────
    CAST(ROUND(d.SimilarityScore * 100, 1) AS NVARCHAR(10)) + '%' AS Similarity,

    -- ── Why are they flagged as duplicates? ──────────────────
    CASE d.DetectionMethod
        WHEN 'SqlFingerprint'    THEN 'Tier 1 – Identical SQL queries (hash match)'
        WHEN 'SqlEmbedding'      THEN 'Tier 2 – Semantically similar SQL queries'
        WHEN 'MetadataEmbedding' THEN 'Tier 3 – Similar report name / description'
    END AS DetectedBy,

    -- ── What action is recommended? ──────────────────────────
    CASE d.RecommendedAction
        WHEN 'Archive' THEN '🔴 Archive — Safe to remove one copy'
        WHEN 'Review'  THEN '🟡 Review  — Needs manual check before action'
        WHEN 'Keep'    THEN '🟢 Keep    — Both should be retained'
    END AS Action,

    -- ── Report 1 ─────────────────────────────────────────────
    r1.Id             AS Report1_Id,
    r1.Name           AS Report1_Name,
    r1.Owner          AS Report1_Owner,
    r1.FolderPath     AS Report1_Folder,
    r1.ReportType     AS Report1_Type,
    r1.UsageStatus    AS Report1_UsageStatus,
    ISNULL(CONVERT(NVARCHAR(20), r1.LastRunDate, 103), 'Never run')
                      AS Report1_LastRunDate,
    (SELECT COUNT(*) FROM ReportSQLs WHERE ReportId = r1.Id)
                      AS Report1_SqlQueryCount,

    -- ── Report 2 ─────────────────────────────────────────────
    r2.Id             AS Report2_Id,
    r2.Name           AS Report2_Name,
    r2.Owner          AS Report2_Owner,
    r2.FolderPath     AS Report2_Folder,
    r2.ReportType     AS Report2_Type,
    r2.UsageStatus    AS Report2_UsageStatus,
    ISNULL(CONVERT(NVARCHAR(20), r2.LastRunDate, 103), 'Never run')
                      AS Report2_LastRunDate,
    (SELECT COUNT(*) FROM ReportSQLs WHERE ReportId = r2.Id)
                      AS Report2_SqlQueryCount,

    -- ── Audit ─────────────────────────────────────────────────
    CONVERT(NVARCHAR(20), d.DetectedAt, 120)
                      AS DetectedAt

FROM DuplicateReports d
JOIN Reports r1 ON d.ReportId1 = r1.Id
JOIN Reports r2 ON d.ReportId2 = r2.Id
ORDER BY
    d.SimilarityScore DESC,
    d.DetectionMethod;
GO

-- ============================================================
-- QUERY 2: SUMMARY — How many duplicates per detection tier?
-- ============================================================
SELECT
    CASE DetectionMethod
        WHEN 'SqlFingerprint'    THEN 'Tier 1 – Identical SQL'
        WHEN 'SqlEmbedding'      THEN 'Tier 2 – Similar SQL'
        WHEN 'MetadataEmbedding' THEN 'Tier 3 – Similar Name/Description'
    END                                                AS DetectionTier,
    COUNT(*)                                           AS TotalPairs,
    SUM(CASE WHEN RecommendedAction = 'Archive' THEN 1 ELSE 0 END) AS SafeToArchive,
    SUM(CASE WHEN RecommendedAction = 'Review'  THEN 1 ELSE 0 END) AS NeedsReview,
    CAST(ROUND(AVG(SimilarityScore) * 100, 1) AS NVARCHAR(10)) + '%' AS AvgSimilarity,
    CAST(ROUND(MAX(SimilarityScore) * 100, 1) AS NVARCHAR(10)) + '%' AS MaxSimilarity,
    CAST(ROUND(MIN(SimilarityScore) * 100, 1) AS NVARCHAR(10)) + '%' AS MinSimilarity
FROM DuplicateReports
GROUP BY DetectionMethod
ORDER BY
    CASE DetectionMethod
        WHEN 'SqlFingerprint'    THEN 1
        WHEN 'SqlEmbedding'      THEN 2
        WHEN 'MetadataEmbedding' THEN 3
    END;
GO

-- ============================================================
-- QUERY 3: EXACT DUPLICATES ONLY (safe to archive immediately)
-- Tier 1 with 100% SQL fingerprint match — these are definite
-- duplicates; one copy can be safely retired.
-- ============================================================
SELECT
    ROW_NUMBER() OVER (ORDER BY r1.Name) AS PairNo,
    '100% — Identical SQL'               AS Similarity,
    r1.Id                                AS Report1_Id,
    r1.Name                              AS Report1_Name,
    r1.Owner                             AS Report1_Owner,
    r1.FolderPath                        AS Report1_Folder,
    ISNULL(CONVERT(NVARCHAR(20), r1.LastRunDate, 103), 'Never run')
                                         AS Report1_LastRunDate,
    r2.Id                                AS Report2_Id,
    r2.Name                              AS Report2_Name,
    r2.Owner                             AS Report2_Owner,
    r2.FolderPath                        AS Report2_Folder,
    ISNULL(CONVERT(NVARCHAR(20), r2.LastRunDate, 103), 'Never run')
                                         AS Report2_LastRunDate
FROM DuplicateReports d
JOIN Reports r1 ON d.ReportId1 = r1.Id
JOIN Reports r2 ON d.ReportId2 = r2.Id
WHERE d.DetectionMethod   = 'SqlFingerprint'
  AND d.SimilarityScore   = 1.0
  AND d.RecommendedAction = 'Archive'
ORDER BY r1.Name;
GO

-- ============================================================
-- QUERY 4: REPORTS INVOLVED IN DUPLICATES
-- Shows every individual report that appears in at least one
-- duplicate pair — useful to identify which reports to review.
-- ============================================================
SELECT DISTINCT
    r.Id,
    r.Name,
    r.Owner,
    r.FolderPath     AS Folder,
    r.ReportType     AS Type,
    r.UsageStatus,
    ISNULL(CONVERT(NVARCHAR(20), r.LastRunDate, 103), 'Never run') AS LastRunDate,
    COUNT(d.Id)      AS DuplicatePairCount,
    STRING_AGG(
        CASE d.RecommendedAction
            WHEN 'Archive' THEN 'Archive'
            WHEN 'Review'  THEN 'Review'
            ELSE 'Keep'
        END, ', ')   AS SuggestedActions
FROM Reports r
JOIN (
    SELECT Id, ReportId1 AS ReportId, RecommendedAction FROM DuplicateReports
    UNION ALL
    SELECT Id, ReportId2 AS ReportId, RecommendedAction FROM DuplicateReports
) d ON r.Id = d.ReportId
GROUP BY r.Id, r.Name, r.Owner, r.FolderPath, r.ReportType, r.UsageStatus, r.LastRunDate
ORDER BY DuplicatePairCount DESC, r.Name;
GO
