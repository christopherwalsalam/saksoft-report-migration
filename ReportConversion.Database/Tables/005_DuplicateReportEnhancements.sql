-- ============================================================
-- Duplicate Report Enhancements
-- Add DetectionMethod column to DuplicateReports to record
-- which tier identified the duplicate pair.
-- ============================================================

USE ReportConversionDB;
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'DuplicateReports' AND COLUMN_NAME = 'DetectionMethod'
)
BEGIN
    ALTER TABLE DuplicateReports
    ADD DetectionMethod NVARCHAR(30) NOT NULL DEFAULT 'MetadataEmbedding';

    ALTER TABLE DuplicateReports
    ADD CONSTRAINT CHK_DuplicateReports_DetectionMethod
        CHECK (DetectionMethod IN ('SqlFingerprint', 'SqlEmbedding', 'MetadataEmbedding'));
END
GO
