-- ============================================================
-- Migration 006: Add BackgroundTasks table
-- Purpose: Tracks progress of long-running UI-triggered jobs
--          (Extraction, StaleReport, KpiGrouping, DuplicateDetection, Migration)
--          so the React frontend can poll GET /api/tasks/{taskId}/status
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'BackgroundTasks')
BEGIN
    CREATE TABLE BackgroundTasks
    (
        Id               INT            IDENTITY(1,1) NOT NULL,
        TaskId           NVARCHAR(50)   NOT NULL,           -- 16-char hex GUID used as the polling key
        TaskType         NVARCHAR(50)   NOT NULL,           -- Extraction | StaleReport | KpiGrouping | DuplicateDetection | Migration
        Status           NVARCHAR(20)   NOT NULL DEFAULT 'Pending',  -- Pending | Running | Completed | Failed
        ProgressPercent  INT            NOT NULL DEFAULT 0,
        CurrentStep      NVARCHAR(500)  NULL,
        StartedAt        DATETIME2      NOT NULL DEFAULT GETUTCDATE(),
        CompletedAt      DATETIME2      NULL,
        ErrorMessage     NVARCHAR(2000) NULL,

        CONSTRAINT PK_BackgroundTasks PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_BackgroundTasks_TaskId UNIQUE (TaskId)
    );

    CREATE NONCLUSTERED INDEX IX_BackgroundTasks_TaskId
        ON BackgroundTasks (TaskId)
        INCLUDE (Status, ProgressPercent, CurrentStep, StartedAt, CompletedAt, ErrorMessage);

    PRINT 'BackgroundTasks table created successfully.';
END
ELSE
BEGIN
    PRINT 'BackgroundTasks table already exists — skipping.';
END
