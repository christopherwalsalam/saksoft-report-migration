using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using ReportConversion.Application.Interfaces;
using ReportConversion.Domain.Entities;
using ReportConversion.Domain.Enums;

namespace ReportConversion.Infrastructure.Repositories;

public class MigrationJobRepository : IMigrationJobRepository
{
    private readonly string _connectionString;

    public MigrationJobRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection is not configured");
    }

    private SqlConnection CreateConnection() => new SqlConnection(_connectionString);

    public async Task<IEnumerable<MigrationJob>> GetAllAsync()
    {
        using var conn = CreateConnection();
        return await conn.QueryAsync<MigrationJob>(
            "SELECT * FROM MigrationJobs ORDER BY StartedAt DESC");
    }

    public async Task<MigrationJob?> GetByIdAsync(int id)
    {
        using var conn = CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<MigrationJob>(
            "SELECT * FROM MigrationJobs WHERE Id = @Id", new { Id = id });
    }

    public async Task<MigrationJob?> GetByReportIdAsync(int reportId)
    {
        using var conn = CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<MigrationJob>(
            "SELECT TOP 1 * FROM MigrationJobs WHERE ReportId = @ReportId ORDER BY StartedAt DESC",
            new { ReportId = reportId });
    }

    public async Task<int> InsertAsync(MigrationJob job)
    {
        using var conn = CreateConnection();
        var sql = """
            INSERT INTO MigrationJobs (ReportId, Status, PowerBiReportId, OutputFilePath, BlobStoragePath, ErrorMessage, StartedAt, HangfireJobId)
            VALUES (@ReportId, @Status, @PowerBiReportId, @OutputFilePath, @BlobStoragePath, @ErrorMessage, @StartedAt, @HangfireJobId);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """;
        return await conn.ExecuteScalarAsync<int>(sql, new
        {
            job.ReportId,
            Status = job.Status.ToString(),
            job.PowerBiReportId,
            job.OutputFilePath,
            job.BlobStoragePath,
            job.ErrorMessage,
            job.StartedAt,
            job.HangfireJobId
        });
    }

    public async Task UpdateStatusAsync(int id, MigrationStatus status, string? errorMessage = null, string? outputPath = null, string? blobPath = null)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync(
            "sp_UpdateMigrationStatus",
            new { JobId = id, Status = status.ToString(), ErrorMessage = errorMessage, OutputFilePath = outputPath, BlobStoragePath = blobPath },
            commandType: System.Data.CommandType.StoredProcedure);
    }

    public async Task<(int Total, int Success, int Failed, int Pending, int InProgress)> GetSummaryAsync()
    {
        using var conn = CreateConnection();
        var result = await conn.QueryFirstOrDefaultAsync(
            "SELECT COUNT(*) AS Total, SUM(CASE WHEN Status='Success' THEN 1 ELSE 0 END) AS Success, SUM(CASE WHEN Status='Failed' THEN 1 ELSE 0 END) AS Failed, SUM(CASE WHEN Status='Pending' THEN 1 ELSE 0 END) AS Pending, SUM(CASE WHEN Status='InProgress' THEN 1 ELSE 0 END) AS InProgress FROM MigrationJobs");
        return (result?.Total ?? 0, result?.Success ?? 0, result?.Failed ?? 0, result?.Pending ?? 0, result?.InProgress ?? 0);
    }
}
