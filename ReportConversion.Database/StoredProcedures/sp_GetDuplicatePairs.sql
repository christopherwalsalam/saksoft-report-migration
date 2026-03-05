-- ============================================================
-- sp_GetDuplicatePairs: Get duplicate pairs above threshold
-- ============================================================
CREATE OR ALTER PROCEDURE sp_GetDuplicatePairs
    @MinSimilarity FLOAT = 0.92
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        d.Id,
        d.ReportId1,
        r1.Name AS Report1Name,
        d.ReportId2,
        r2.Name AS Report2Name,
        d.SimilarityScore,
        d.RecommendedAction,
        d.DetectedAt
    FROM DuplicateReports d
    JOIN Reports r1 ON d.ReportId1 = r1.Id
    JOIN Reports r2 ON d.ReportId2 = r2.Id
    WHERE d.SimilarityScore >= @MinSimilarity
    ORDER BY d.SimilarityScore DESC;
END
GO
