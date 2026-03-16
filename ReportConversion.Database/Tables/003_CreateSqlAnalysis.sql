-- ============================================================
-- ReportSqlAnalysis Table
-- Stores the technical SQL analysis per report used for
-- SQL-derived KPI grouping (tables, columns, aggregations, etc.)
-- ============================================================

USE ReportConversionDB;
GO

IF OBJECT_ID('ReportSqlAnalysis', 'U') IS NOT NULL DROP TABLE ReportSqlAnalysis;
CREATE TABLE ReportSqlAnalysis (
    Id                   INT IDENTITY(1,1) PRIMARY KEY,
    ReportId             INT NOT NULL,
    TablesReferenced     NVARCHAR(MAX) NULL,    -- comma-separated table names from FROM/JOIN
    ColumnsUsed          NVARCHAR(MAX) NULL,    -- key columns from SELECT / WHERE / GROUP BY
    AggregationPatterns  NVARCHAR(MAX) NULL,    -- SUM, COUNT, AVG, MAX, MIN, GROUP BY usage
    JoinPatterns         NVARCHAR(MAX) NULL,    -- join types and joined table pairs
    FilterPatterns       NVARCHAR(MAX) NULL,    -- WHERE clause keyword patterns
    SqlSummary           NVARCHAR(MAX) NULL,    -- compact text summary sent to LLM
    AnalysedAt           DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_ReportSqlAnalysis_Reports FOREIGN KEY (ReportId) REFERENCES Reports(Id) ON DELETE CASCADE,
    CONSTRAINT UQ_ReportSqlAnalysis_ReportId UNIQUE (ReportId)
);
CREATE INDEX IX_ReportSqlAnalysis_ReportId ON ReportSqlAnalysis(ReportId);
GO
