-- ============================================================
-- ReportConversion Database Schema
-- SQL Server 2022
-- ============================================================

USE ReportConversionDB;
GO

-- ============================================================
-- KpiGroups
-- ============================================================
IF OBJECT_ID('KpiGroups', 'U') IS NOT NULL DROP TABLE KpiGroups;
CREATE TABLE KpiGroups (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    Name        NVARCHAR(200) NOT NULL,
    Description NVARCHAR(1000) NULL,
    CreatedAt   DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT UQ_KpiGroups_Name UNIQUE (Name)
);
GO

-- ============================================================
-- Reports
-- ============================================================
IF OBJECT_ID('Reports', 'U') IS NOT NULL DROP TABLE Reports;
CREATE TABLE Reports (
    Id                     INT IDENTITY(1,1) PRIMARY KEY,
    SapReportId            NVARCHAR(100) NOT NULL,
    Name                   NVARCHAR(500) NOT NULL,
    Description            NVARCHAR(MAX) NULL,
    Owner                  NVARCHAR(200) NULL,
    CreatedDate            DATETIME2 NOT NULL,
    LastModifiedDate       DATETIME2 NOT NULL,
    LastRunDate            DATETIME2 NULL,
    ReportType             NVARCHAR(50) NOT NULL DEFAULT 'Unknown',
    FolderPath             NVARCHAR(1000) NULL,
    Category               NVARCHAR(200) NULL,
    IsScheduled            BIT NOT NULL DEFAULT 0,
    HasActiveSubscriptions BIT NOT NULL DEFAULT 0,
    UsageStatus            NVARCHAR(20) NOT NULL DEFAULT 'Active',
    KpiGroupId             INT NULL,
    KpiGroupName           AS (SELECT Name FROM KpiGroups WHERE Id = KpiGroupId) PERSISTED,
    EmbeddingVector        NVARCHAR(MAX) NULL,
    ExtractedAt            DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt              DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_Reports_KpiGroups FOREIGN KEY (KpiGroupId) REFERENCES KpiGroups(Id),
    CONSTRAINT UQ_Reports_SapReportId UNIQUE (SapReportId),
    CONSTRAINT CHK_Reports_UsageStatus CHECK (UsageStatus IN ('Active', 'Stale', 'NeverUsed')),
    CONSTRAINT CHK_Reports_ReportType CHECK (ReportType IN ('CrystalReport', 'WebIntelligence', 'Unknown'))
);
CREATE INDEX IX_Reports_UsageStatus ON Reports(UsageStatus);
CREATE INDEX IX_Reports_KpiGroupId ON Reports(KpiGroupId);
CREATE INDEX IX_Reports_LastRunDate ON Reports(LastRunDate);
CREATE INDEX IX_Reports_ReportType ON Reports(ReportType);
GO

-- ============================================================
-- ReportElements
-- ============================================================
IF OBJECT_ID('ReportElements', 'U') IS NOT NULL DROP TABLE ReportElements;
CREATE TABLE ReportElements (
    Id               INT IDENTITY(1,1) PRIMARY KEY,
    ReportId         INT NOT NULL,
    ElementType      NVARCHAR(100) NOT NULL,
    ElementName      NVARCHAR(500) NULL,
    Properties       NVARCHAR(MAX) NULL,
    PowerBiEquivalent NVARCHAR(200) NULL,
    CONSTRAINT FK_ReportElements_Reports FOREIGN KEY (ReportId) REFERENCES Reports(Id) ON DELETE CASCADE
);
CREATE INDEX IX_ReportElements_ReportId ON ReportElements(ReportId);
GO

-- ============================================================
-- ReportSQLs
-- ============================================================
IF OBJECT_ID('ReportSQLs', 'U') IS NOT NULL DROP TABLE ReportSQLs;
CREATE TABLE ReportSQLs (
    Id             INT IDENTITY(1,1) PRIMARY KEY,
    ReportId       INT NOT NULL,
    SqlText        NVARCHAR(MAX) NULL,
    SqlFingerprint NVARCHAR(32) NULL,
    QueryName      NVARCHAR(500) NULL,
    CONSTRAINT FK_ReportSQLs_Reports FOREIGN KEY (ReportId) REFERENCES Reports(Id) ON DELETE CASCADE
);
CREATE INDEX IX_ReportSQLs_ReportId ON ReportSQLs(ReportId);
CREATE INDEX IX_ReportSQLs_Fingerprint ON ReportSQLs(SqlFingerprint);
GO

-- ============================================================
-- ReportDataSources
-- ============================================================
IF OBJECT_ID('ReportDataSources', 'U') IS NOT NULL DROP TABLE ReportDataSources;
CREATE TABLE ReportDataSources (
    Id               INT IDENTITY(1,1) PRIMARY KEY,
    ReportId         INT NOT NULL,
    DataSourceName   NVARCHAR(300) NOT NULL,
    ConnectionString NVARCHAR(MAX) NULL,
    ServerName       NVARCHAR(300) NULL,
    DatabaseName     NVARCHAR(300) NULL,
    DataSourceType   NVARCHAR(100) NULL,
    CONSTRAINT FK_ReportDataSources_Reports FOREIGN KEY (ReportId) REFERENCES Reports(Id) ON DELETE CASCADE
);
CREATE INDEX IX_ReportDataSources_ReportId ON ReportDataSources(ReportId);
GO

-- ============================================================
-- KpiRegistry
-- ============================================================
IF OBJECT_ID('KpiRegistry', 'U') IS NOT NULL DROP TABLE KpiRegistry;
CREATE TABLE KpiRegistry (
    Id                   INT IDENTITY(1,1) PRIMARY KEY,
    KpiName              NVARCHAR(300) NOT NULL,
    KpiGroupId           INT NOT NULL,
    BusinessDomain       NVARCHAR(200) NULL,
    MetricsInvolved      NVARCHAR(MAX) NULL,
    ReportCount          INT NOT NULL DEFAULT 0,
    DataSourcesInvolved  NVARCHAR(MAX) NULL,
    CreatedAt            DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_KpiRegistry_KpiGroups FOREIGN KEY (KpiGroupId) REFERENCES KpiGroups(Id),
    CONSTRAINT UQ_KpiRegistry_Name_Group UNIQUE (KpiName, KpiGroupId)
);
CREATE INDEX IX_KpiRegistry_KpiGroupId ON KpiRegistry(KpiGroupId);
GO

-- ============================================================
-- DuplicateReports
-- ============================================================
IF OBJECT_ID('DuplicateReports', 'U') IS NOT NULL DROP TABLE DuplicateReports;
CREATE TABLE DuplicateReports (
    Id                INT IDENTITY(1,1) PRIMARY KEY,
    ReportId1         INT NOT NULL,
    ReportId2         INT NOT NULL,
    SimilarityScore   FLOAT NOT NULL,
    RecommendedAction NVARCHAR(20) NOT NULL DEFAULT 'Review',
    DetectedAt        DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_DuplicateReports_Report1 FOREIGN KEY (ReportId1) REFERENCES Reports(Id),
    CONSTRAINT FK_DuplicateReports_Report2 FOREIGN KEY (ReportId2) REFERENCES Reports(Id),
    CONSTRAINT CHK_DuplicateReports_Action CHECK (RecommendedAction IN ('Keep', 'Archive', 'Review')),
    CONSTRAINT CHK_DuplicateReports_Score CHECK (SimilarityScore BETWEEN 0.0 AND 1.0)
);
CREATE INDEX IX_DuplicateReports_ReportId1 ON DuplicateReports(ReportId1);
CREATE INDEX IX_DuplicateReports_ReportId2 ON DuplicateReports(ReportId2);
CREATE INDEX IX_DuplicateReports_Score ON DuplicateReports(SimilarityScore DESC);
GO

-- ============================================================
-- MigrationJobs
-- ============================================================
IF OBJECT_ID('MigrationJobs', 'U') IS NOT NULL DROP TABLE MigrationJobs;
CREATE TABLE MigrationJobs (
    Id               INT IDENTITY(1,1) PRIMARY KEY,
    ReportId         INT NOT NULL,
    Status           NVARCHAR(20) NOT NULL DEFAULT 'Pending',
    PowerBiReportId  NVARCHAR(200) NULL,
    OutputFilePath   NVARCHAR(1000) NULL,
    BlobStoragePath  NVARCHAR(1000) NULL,
    ErrorMessage     NVARCHAR(MAX) NULL,
    StartedAt        DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CompletedAt      DATETIME2 NULL,
    HangfireJobId    NVARCHAR(100) NULL,
    CONSTRAINT FK_MigrationJobs_Reports FOREIGN KEY (ReportId) REFERENCES Reports(Id),
    CONSTRAINT CHK_MigrationJobs_Status CHECK (Status IN ('Pending', 'InProgress', 'Success', 'Failed', 'Partial'))
);
CREATE INDEX IX_MigrationJobs_ReportId ON MigrationJobs(ReportId);
CREATE INDEX IX_MigrationJobs_Status ON MigrationJobs(Status);
GO

-- ============================================================
-- MigrationLogs
-- ============================================================
IF OBJECT_ID('MigrationLogs', 'U') IS NOT NULL DROP TABLE MigrationLogs;
CREATE TABLE MigrationLogs (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    MigrationJobId  INT NOT NULL,
    ReportId        INT NOT NULL,
    LogLevel        NVARCHAR(20) NOT NULL DEFAULT 'Info',
    Message         NVARCHAR(2000) NOT NULL,
    Details         NVARCHAR(MAX) NULL,
    Timestamp       DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_MigrationLogs_Jobs FOREIGN KEY (MigrationJobId) REFERENCES MigrationJobs(Id) ON DELETE CASCADE,
    CONSTRAINT FK_MigrationLogs_Reports FOREIGN KEY (ReportId) REFERENCES Reports(Id),
    CONSTRAINT CHK_MigrationLogs_Level CHECK (LogLevel IN ('Debug', 'Info', 'Warning', 'Error'))
);
CREATE INDEX IX_MigrationLogs_JobId ON MigrationLogs(MigrationJobId);
CREATE INDEX IX_MigrationLogs_ReportId ON MigrationLogs(ReportId);
CREATE INDEX IX_MigrationLogs_Timestamp ON MigrationLogs(Timestamp DESC);
GO
