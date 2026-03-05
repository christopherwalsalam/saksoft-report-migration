-- ============================================================
-- sp_UpdateMigrationStatus
-- ============================================================
CREATE OR ALTER PROCEDURE sp_UpdateMigrationStatus
    @JobId          INT,
    @Status         NVARCHAR(20),
    @ErrorMessage   NVARCHAR(MAX) = NULL,
    @OutputFilePath NVARCHAR(1000) = NULL,
    @BlobStoragePath NVARCHAR(1000) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @CompletedAt DATETIME2 = NULL;
    IF @Status IN ('Success', 'Failed', 'Partial')
        SET @CompletedAt = GETUTCDATE();

    UPDATE MigrationJobs
    SET Status          = @Status,
        ErrorMessage    = ISNULL(@ErrorMessage, ErrorMessage),
        OutputFilePath  = ISNULL(@OutputFilePath, OutputFilePath),
        BlobStoragePath = ISNULL(@BlobStoragePath, BlobStoragePath),
        CompletedAt     = ISNULL(@CompletedAt, CompletedAt)
    WHERE Id = @JobId;
END
GO
