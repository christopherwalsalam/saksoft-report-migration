using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using ReportConversion.Application.Interfaces;
using ReportConversion.Domain.Entities;

namespace ReportConversion.Infrastructure.Repositories;

public class MigrationLogRepository : IMigrationLogRepository
{
    private readonly string _connectionString;

    public MigrationLogRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection is not configured");
    }

    private SqlConnection CreateConnection() => new SqlConnection(_connectionString);

    public async Task<IEnumerable<MigrationLog>> GetByReportIdAsync(int reportId)
    {
        using var conn = CreateConnection();
        return await conn.QueryAsync<MigrationLog>(
            "SELECT * FROM MigrationLogs WHERE ReportId = @ReportId ORDER BY Timestamp DESC",
            new { ReportId = reportId });
    }

    public async Task<IEnumerable<MigrationLog>> GetByJobIdAsync(int jobId)
    {
        using var conn = CreateConnection();
        return await conn.QueryAsync<MigrationLog>(
            "SELECT * FROM MigrationLogs WHERE MigrationJobId = @JobId ORDER BY Timestamp",
            new { JobId = jobId });
    }

    public async Task<int> InsertAsync(MigrationLog log)
    {
        using var conn = CreateConnection();
        var sql = """
            INSERT INTO MigrationLogs (MigrationJobId, ReportId, LogLevel, Message, Details, Timestamp)
            VALUES (@MigrationJobId, @ReportId, @LogLevel, @Message, @Details, @Timestamp);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """;
        return await conn.ExecuteScalarAsync<int>(sql, log);
    }
}
