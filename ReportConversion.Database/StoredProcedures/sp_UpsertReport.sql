-- ============================================================
-- sp_UpsertReport: Insert or update report metadata
-- ============================================================
CREATE OR ALTER PROCEDURE sp_UpsertReport
    @SapReportId            NVARCHAR(100),
    @Name                   NVARCHAR(500),
    @Description            NVARCHAR(MAX),
    @Owner                  NVARCHAR(200),
    @CreatedDate            DATETIME2,
    @LastModifiedDate       DATETIME2,
    @LastRunDate            DATETIME2 = NULL,
    @ReportType             NVARCHAR(50),
    @FolderPath             NVARCHAR(1000) = NULL,
    @Category               NVARCHAR(200) = NULL,
    @IsScheduled            BIT = 0,
    @HasActiveSubscriptions BIT = 0,
    @UsageStatus            NVARCHAR(20) = 'Active',
    @ExtractedAt            DATETIME2 = NULL,
    @UpdatedAt              DATETIME2 = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Id INT;
    DECLARE @Now DATETIME2 = GETUTCDATE();

    IF @ExtractedAt IS NULL SET @ExtractedAt = @Now;
    IF @UpdatedAt IS NULL SET @UpdatedAt = @Now;

    IF EXISTS (SELECT 1 FROM Reports WHERE SapReportId = @SapReportId)
    BEGIN
        UPDATE Reports
        SET Name                   = @Name,
            Description            = @Description,
            Owner                  = @Owner,
            CreatedDate            = @CreatedDate,
            LastModifiedDate       = @LastModifiedDate,
            LastRunDate            = @LastRunDate,
            ReportType             = @ReportType,
            FolderPath             = @FolderPath,
            Category               = @Category,
            IsScheduled            = @IsScheduled,
            HasActiveSubscriptions = @HasActiveSubscriptions,
            UsageStatus            = @UsageStatus,
            UpdatedAt              = @UpdatedAt
        WHERE SapReportId = @SapReportId;

        SELECT Id FROM Reports WHERE SapReportId = @SapReportId;
    END
    ELSE
    BEGIN
        INSERT INTO Reports (
            SapReportId, Name, Description, Owner, CreatedDate, LastModifiedDate,
            LastRunDate, ReportType, FolderPath, Category, IsScheduled,
            HasActiveSubscriptions, UsageStatus, ExtractedAt, UpdatedAt
        )
        VALUES (
            @SapReportId, @Name, @Description, @Owner, @CreatedDate, @LastModifiedDate,
            @LastRunDate, @ReportType, @FolderPath, @Category, @IsScheduled,
            @HasActiveSubscriptions, @UsageStatus, @ExtractedAt, @UpdatedAt
        );

        SELECT SCOPE_IDENTITY();
    END
END
GO
