-- ============================================================
-- KPI Group Report Analysis Queries
-- Run each section independently in SSMS / Azure Data Studio.
-- ============================================================

USE ReportConversionDB;
GO

-- ============================================================
-- QUERY 1: ALL GROUPS WITH THEIR REPORTS
-- One row per report, grouped under its KPI group.
-- Shows the group name, reason for grouping, and full
-- details of every report inside it.
-- ============================================================
SELECT
    -- ── Group identity ────────────────────────────────────────
    kg.Id                                               AS GroupId,
    kg.Name                                             AS GroupName,
    kg.Description                                      AS GroupDescription,
    kg.Reason                                           AS WhyGroupedTogether,

    -- ── Group stats (computed per group) ─────────────────────
    COUNT(r.Id) OVER (PARTITION BY kg.Id)               AS TotalReportsInGroup,

    -- ── Report details ────────────────────────────────────────
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
    CONVERT(NVARCHAR(20), kg.CreatedAt, 120)            AS GroupCreatedAt

FROM KpiGroups kg
JOIN ReportKpiGroupMapping m ON kg.Id = m.KpiGroupId
JOIN Reports r               ON m.ReportId = r.Id
ORDER BY
    kg.Name,
    r.Name;
GO

-- ============================================================
-- QUERY 2: GROUP SUMMARY
-- One row per KPI group — how many reports, what types,
-- usage breakdown, and the grouping reason at a glance.
-- ============================================================
SELECT
    kg.Id                                               AS GroupId,
    kg.Name                                             AS GroupName,
    kg.Description                                      AS GroupDescription,
    kg.Reason                                           AS WhyGroupedTogether,

    -- Report counts
    COUNT(r.Id)                                         AS TotalReports,
    SUM(CASE WHEN r.UsageStatus = 'Active'    THEN 1 ELSE 0 END) AS ActiveReports,
    SUM(CASE WHEN r.UsageStatus = 'Stale'     THEN 1 ELSE 0 END) AS StaleReports,
    SUM(CASE WHEN r.UsageStatus = 'NeverUsed' THEN 1 ELSE 0 END) AS NeverUsedReports,

    -- Report types
    SUM(CASE WHEN r.ReportType = 'CrystalReport'     THEN 1 ELSE 0 END) AS CrystalReports,
    SUM(CASE WHEN r.ReportType = 'WebIntelligence'   THEN 1 ELSE 0 END) AS WebIntelligenceReports,

    -- Scheduling
    SUM(CASE WHEN r.IsScheduled = 1 THEN 1 ELSE 0 END) AS ScheduledReports,

    -- Last activity
    ISNULL(CONVERT(NVARCHAR(20), MAX(r.LastRunDate), 103), 'Never run')
                                                        AS MostRecentRunDate,
    ISNULL(CONVERT(NVARCHAR(20), MIN(r.LastRunDate), 103), 'Never run')
                                                        AS OldestRunDate,

    CONVERT(NVARCHAR(20), kg.CreatedAt, 120)            AS GroupCreatedAt

FROM KpiGroups kg
JOIN ReportKpiGroupMapping m ON kg.Id = m.KpiGroupId
JOIN Reports r               ON m.ReportId = r.Id
GROUP BY
    kg.Id, kg.Name, kg.Description, kg.Reason, kg.CreatedAt
ORDER BY
    TotalReports DESC,
    kg.Name;
GO

-- ============================================================
-- QUERY 3: UNGROUPED REPORTS
-- Reports that have not been assigned to any KPI group yet.
-- These should be re-run through the grouping pipeline.
-- ============================================================
SELECT
    r.Id                                                AS ReportId,
    r.Name                                              AS ReportName,
    r.Owner                                             AS Owner,
    r.FolderPath                                        AS Folder,
    r.ReportType                                        AS Type,
    r.UsageStatus                                       AS UsageStatus,
    ISNULL(CONVERT(NVARCHAR(20), r.LastRunDate, 103), 'Never run')
                                                        AS LastRunDate,
    (SELECT COUNT(*) FROM ReportSQLs WHERE ReportId = r.Id)
                                                        AS SqlQueryCount,
    'Not grouped — run KPI grouping pipeline'           AS Note

FROM Reports r
WHERE NOT EXISTS (
    SELECT 1 FROM ReportKpiGroupMapping m WHERE m.ReportId = r.Id
)
ORDER BY r.Name;
GO

-- ============================================================
-- QUERY 4: REPORTS PER GROUP — PIVOT / FLAT LIST
-- Compact view: one row per group showing all report names
-- as a comma-separated list. Good for a quick overview.
-- ============================================================
SELECT
    kg.Id                                               AS GroupId,
    kg.Name                                             AS GroupName,
    COUNT(r.Id)                                         AS ReportCount,
    STRING_AGG(r.Name, ' | ')
        WITHIN GROUP (ORDER BY r.Name)                  AS Reports,
    kg.Reason                                           AS WhyGroupedTogether

FROM KpiGroups kg
JOIN ReportKpiGroupMapping m ON kg.Id = m.KpiGroupId
JOIN Reports r               ON m.ReportId = r.Id
GROUP BY
    kg.Id, kg.Name, kg.Reason
ORDER BY
    ReportCount DESC,
    kg.Name;
GO
