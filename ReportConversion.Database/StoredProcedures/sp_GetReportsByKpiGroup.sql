-- ============================================================
-- sp_GetReportsByKpiGroup
-- Returns all reports for a given KPI group (or all groups)
-- with full details in a human-readable format.
--
-- Parameters:
--   @KpiGroupId  INT           (default NULL) — specific group; NULL = all groups
--   @UsageStatus NVARCHAR(20)  (default NULL) — filter: 'Active' | 'Stale' | 'NeverUsed' | NULL (all)
-- ============================================================
CREATE OR ALTER PROCEDURE sp_GetReportsByKpiGroup
    @KpiGroupId  INT          = NULL,
    @UsageStatus NVARCHAR(20) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        -- ── Group ────────────────────────────────────────────
        kg.Id                                               AS GroupId,
        kg.Name                                             AS GroupName,
        kg.Description                                      AS GroupDescription,
        kg.Reason                                           AS WhyGroupedTogether,
        COUNT(r.Id) OVER (PARTITION BY kg.Id)               AS TotalReportsInGroup,

        -- ── Report ───────────────────────────────────────────
        r.Id                                                AS ReportId,
        r.Name                                              AS ReportName,
        r.Owner                                             AS ReportOwner,
        r.FolderPath                                        AS Folder,
        r.ReportType                                        AS Type,
        r.UsageStatus                                       AS UsageStatus,
        CASE r.IsScheduled
            WHEN 1 THEN 'Yes' ELSE 'No'
        END                                                 AS IsScheduled,
        ISNULL(CONVERT(NVARCHAR(20), r.LastRunDate, 103), 'Never run')
                                                            AS LastRunDate,
        (SELECT COUNT(*) FROM ReportSQLs WHERE ReportId = r.Id)
                                                            AS SqlQueryCount,
        CONVERT(NVARCHAR(20), m.AssignedAt, 120)            AS AssignedAt

    FROM KpiGroups kg
    JOIN ReportKpiGroupMapping m ON kg.Id  = m.KpiGroupId
    JOIN Reports r               ON m.ReportId = r.Id
    WHERE (@KpiGroupId  IS NULL OR kg.Id          = @KpiGroupId)
      AND (@UsageStatus IS NULL OR r.UsageStatus  = @UsageStatus)
    ORDER BY
        kg.Name,
        r.Name;
END
GO
