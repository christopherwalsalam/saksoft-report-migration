-- ============================================================
-- sp_GetMigrationSummary
-- ============================================================
CREATE OR ALTER PROCEDURE sp_GetMigrationSummary
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        COUNT(*)                                                    AS Total,
        SUM(CASE WHEN Status = 'Success'    THEN 1 ELSE 0 END)    AS SuccessCount,
        SUM(CASE WHEN Status = 'Failed'     THEN 1 ELSE 0 END)    AS FailedCount,
        SUM(CASE WHEN Status = 'Pending'    THEN 1 ELSE 0 END)    AS PendingCount,
        SUM(CASE WHEN Status = 'InProgress' THEN 1 ELSE 0 END)    AS InProgressCount,
        SUM(CASE WHEN Status = 'Partial'    THEN 1 ELSE 0 END)    AS PartialCount,
        MIN(StartedAt)                                              AS FirstJobStarted,
        MAX(CompletedAt)                                            AS LastJobCompleted
    FROM MigrationJobs;

    -- Per KPI Group breakdown
    SELECT
        ISNULL(kg.Name, 'Unclassified') AS KpiGroup,
        COUNT(mj.Id)                     AS TotalJobs,
        SUM(CASE WHEN mj.Status = 'Success' THEN 1 ELSE 0 END) AS Succeeded,
        SUM(CASE WHEN mj.Status = 'Failed'  THEN 1 ELSE 0 END) AS Failed
    FROM MigrationJobs mj
    JOIN Reports r ON mj.ReportId = r.Id
    LEFT JOIN KpiGroups kg ON r.KpiGroupId = kg.Id
    GROUP BY kg.Name
    ORDER BY kg.Name;
END
GO
