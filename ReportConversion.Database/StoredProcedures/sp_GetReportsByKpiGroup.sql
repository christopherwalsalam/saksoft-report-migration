-- ============================================================
-- sp_GetReportsByKpiGroup
-- ============================================================
CREATE OR ALTER PROCEDURE sp_GetReportsByKpiGroup
    @KpiGroupId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT r.*, kg.Name AS KpiGroupName
    FROM Reports r
    LEFT JOIN KpiGroups kg ON r.KpiGroupId = kg.Id
    WHERE r.KpiGroupId = @KpiGroupId
    ORDER BY r.Name ASC;
END
GO
