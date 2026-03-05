-- ============================================================
-- sp_GetReportsByUsageStatus
-- ============================================================
CREATE OR ALTER PROCEDURE sp_GetReportsByUsageStatus
    @UsageStatus NVARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT r.*, kg.Name AS KpiGroupName
    FROM Reports r
    LEFT JOIN KpiGroups kg ON r.KpiGroupId = kg.Id
    WHERE r.UsageStatus = @UsageStatus
    ORDER BY r.LastRunDate ASC, r.Name ASC;
END
GO
