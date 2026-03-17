-- ============================================================
-- sp_GetDuplicatePairs
-- Returns duplicate report pairs in a format that is easy
-- for users to read and act on.
--
-- Parameters:
--   @MinSimilarity  FLOAT        (default 0.80) — exclude pairs below this score
--   @DetectionMethod NVARCHAR(30) (default NULL) — filter by tier:
--                    'SqlFingerprint' | 'SqlEmbedding' | 'MetadataEmbedding' | NULL (all)
--   @ActionFilter   NVARCHAR(20)  (default NULL) — filter by action:
--                    'Archive' | 'Review' | 'Keep' | NULL (all)
-- ============================================================
CREATE OR ALTER PROCEDURE sp_GetDuplicatePairs
    @MinSimilarity   FLOAT        = 0.80,
    @DetectionMethod NVARCHAR(30) = NULL,
    @ActionFilter    NVARCHAR(20) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        ROW_NUMBER() OVER (ORDER BY d.SimilarityScore DESC) AS PairNo,

        -- Similarity as a readable percentage
        CAST(ROUND(d.SimilarityScore * 100, 1) AS NVARCHAR(10)) + '%'
                                                    AS Similarity,

        -- Plain-English label for the detection method
        CASE d.DetectionMethod
            WHEN 'SqlFingerprint'    THEN 'Tier 1 – Identical SQL queries'
            WHEN 'SqlEmbedding'      THEN 'Tier 2 – Semantically similar SQL'
            WHEN 'MetadataEmbedding' THEN 'Tier 3 – Similar name / description'
        END                                         AS DetectedBy,

        -- Recommended action in plain English
        CASE d.RecommendedAction
            WHEN 'Archive' THEN 'Archive – Safe to remove one copy'
            WHEN 'Review'  THEN 'Review  – Manual check needed'
            WHEN 'Keep'    THEN 'Keep    – Retain both'
        END                                         AS Action,

        -- Report 1
        r1.Id                                       AS Report1_Id,
        r1.Name                                     AS Report1_Name,
        r1.Owner                                    AS Report1_Owner,
        r1.FolderPath                               AS Report1_Folder,
        r1.ReportType                               AS Report1_Type,
        r1.UsageStatus                              AS Report1_UsageStatus,
        ISNULL(CONVERT(NVARCHAR(20), r1.LastRunDate, 103), 'Never run')
                                                    AS Report1_LastRunDate,
        (SELECT COUNT(*) FROM ReportSQLs WHERE ReportId = r1.Id)
                                                    AS Report1_SqlQueries,

        -- Report 2
        r2.Id                                       AS Report2_Id,
        r2.Name                                     AS Report2_Name,
        r2.Owner                                    AS Report2_Owner,
        r2.FolderPath                               AS Report2_Folder,
        r2.ReportType                               AS Report2_Type,
        r2.UsageStatus                              AS Report2_UsageStatus,
        ISNULL(CONVERT(NVARCHAR(20), r2.LastRunDate, 103), 'Never run')
                                                    AS Report2_LastRunDate,
        (SELECT COUNT(*) FROM ReportSQLs WHERE ReportId = r2.Id)
                                                    AS Report2_SqlQueries,

        CONVERT(NVARCHAR(20), d.DetectedAt, 120)    AS DetectedAt

    FROM DuplicateReports d
    JOIN Reports r1 ON d.ReportId1 = r1.Id
    JOIN Reports r2 ON d.ReportId2 = r2.Id
    WHERE d.SimilarityScore >= @MinSimilarity
      AND (@DetectionMethod IS NULL OR d.DetectionMethod = @DetectionMethod)
      AND (@ActionFilter    IS NULL OR d.RecommendedAction = @ActionFilter)
    ORDER BY d.SimilarityScore DESC;
END
GO
