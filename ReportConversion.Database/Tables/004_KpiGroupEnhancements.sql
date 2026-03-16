-- ============================================================
-- KPI Group Enhancements
-- 1. Add Reason column to KpiGroups
-- 2. Create ReportKpiGroupMapping table
-- ============================================================

USE ReportConversionDB;
GO

-- ============================================================
-- 1. Add Reason column to KpiGroups
--    Stores the LLM-generated explanation of WHY the reports
--    in this group share the same KPI (e.g. common tables,
--    aggregation patterns, calculation logic).
-- ============================================================
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'KpiGroups' AND COLUMN_NAME = 'Reason'
)
BEGIN
    ALTER TABLE KpiGroups
    ADD Reason NVARCHAR(MAX) NULL;
END
GO

-- ============================================================
-- 2. ReportKpiGroupMapping
--    Explicit many-to-one mapping between Reports and KpiGroups.
--    Provides a queryable table to see exactly which reports
--    belong to each KPI group, alongside when they were assigned.
-- ============================================================
IF OBJECT_ID('ReportKpiGroupMapping', 'U') IS NOT NULL DROP TABLE ReportKpiGroupMapping;
CREATE TABLE ReportKpiGroupMapping (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    ReportId    INT NOT NULL,
    KpiGroupId  INT NOT NULL,
    AssignedAt  DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_ReportKpiGroupMapping_Reports    FOREIGN KEY (ReportId)   REFERENCES Reports(Id)   ON DELETE CASCADE,
    CONSTRAINT FK_ReportKpiGroupMapping_KpiGroups  FOREIGN KEY (KpiGroupId) REFERENCES KpiGroups(Id) ON DELETE CASCADE,
    CONSTRAINT UQ_ReportKpiGroupMapping_Pair       UNIQUE (ReportId, KpiGroupId)
);
CREATE INDEX IX_ReportKpiGroupMapping_ReportId   ON ReportKpiGroupMapping(ReportId);
CREATE INDEX IX_ReportKpiGroupMapping_KpiGroupId ON ReportKpiGroupMapping(KpiGroupId);
GO
