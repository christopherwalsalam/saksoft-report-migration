using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using ReportConversion.Application.Interfaces;
using ReportConversion.Domain.Entities;

namespace ReportConversion.Infrastructure.Repositories;

public class ReportSqlRepository : IReportSqlRepository
{
    private readonly string _connectionString;

    public ReportSqlRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection is not configured");
    }

    private SqlConnection CreateConnection() => new SqlConnection(_connectionString);

    public async Task<IEnumerable<ReportSql>> GetByReportIdAsync(int reportId)
    {
        using var conn = CreateConnection();
        return await conn.QueryAsync<ReportSql>(
            "SELECT * FROM ReportSQLs WHERE ReportId = @ReportId",
            new { ReportId = reportId });
    }

    public async Task<IEnumerable<ReportSql>> GetAllAsync()
    {
        using var conn = CreateConnection();
        return await conn.QueryAsync<ReportSql>(
            "SELECT Id, ReportId, SqlText, SqlFingerprint, QueryName FROM ReportSQLs ORDER BY ReportId");
    }

    public async Task<int> InsertAsync(ReportSql reportSql)
    {
        using var conn = CreateConnection();
        var sql = """
            INSERT INTO ReportSQLs (ReportId, SqlText, SqlFingerprint, QueryName)
            VALUES (@ReportId, @SqlText, @SqlFingerprint, @QueryName);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """;
        return await conn.ExecuteScalarAsync<int>(sql, reportSql);
    }

    public async Task DeleteByReportIdAsync(int reportId)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync("DELETE FROM ReportSQLs WHERE ReportId = @ReportId", new { ReportId = reportId });
    }
}
