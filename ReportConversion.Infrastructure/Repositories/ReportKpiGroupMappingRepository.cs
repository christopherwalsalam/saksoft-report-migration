using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using ReportConversion.Application.Interfaces;
using ReportConversion.Domain.Entities;

namespace ReportConversion.Infrastructure.Repositories;

public class ReportKpiGroupMappingRepository : IReportKpiGroupMappingRepository
{
    private readonly string _connectionString;

    public ReportKpiGroupMappingRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection is not configured");
    }

    private SqlConnection CreateConnection() => new SqlConnection(_connectionString);

    public async Task<IEnumerable<ReportKpiGroupMapping>> GetByKpiGroupIdAsync(int kpiGroupId)
    {
        using var conn = CreateConnection();
        return await conn.QueryAsync<ReportKpiGroupMapping>(
            "SELECT * FROM ReportKpiGroupMapping WHERE KpiGroupId = @KpiGroupId ORDER BY ReportId",
            new { KpiGroupId = kpiGroupId });
    }

    public async Task<ReportKpiGroupMapping?> GetByReportIdAsync(int reportId)
    {
        using var conn = CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<ReportKpiGroupMapping>(
            "SELECT * FROM ReportKpiGroupMapping WHERE ReportId = @ReportId",
            new { ReportId = reportId });
    }

    public async Task<IEnumerable<ReportKpiGroupMapping>> GetAllAsync()
    {
        using var conn = CreateConnection();
        return await conn.QueryAsync<ReportKpiGroupMapping>(
            "SELECT * FROM ReportKpiGroupMapping ORDER BY KpiGroupId, ReportId");
    }

    public async Task<int> InsertAsync(ReportKpiGroupMapping mapping)
    {
        using var conn = CreateConnection();
        // IF NOT EXISTS guard makes this idempotent — safe to call on re-runs
        var sql = """
            IF NOT EXISTS (
                SELECT 1 FROM ReportKpiGroupMapping
                WHERE ReportId = @ReportId AND KpiGroupId = @KpiGroupId
            )
            BEGIN
                INSERT INTO ReportKpiGroupMapping (ReportId, KpiGroupId, AssignedAt)
                VALUES (@ReportId, @KpiGroupId, @AssignedAt);
                SELECT CAST(SCOPE_IDENTITY() AS INT);
            END
            ELSE
            BEGIN
                SELECT Id FROM ReportKpiGroupMapping
                WHERE ReportId = @ReportId AND KpiGroupId = @KpiGroupId;
            END
            """;
        return await conn.ExecuteScalarAsync<int>(sql, mapping);
    }

    public async Task DeleteByReportIdAsync(int reportId)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync(
            "DELETE FROM ReportKpiGroupMapping WHERE ReportId = @ReportId",
            new { ReportId = reportId });
    }

    public async Task DeleteByKpiGroupIdAsync(int kpiGroupId)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync(
            "DELETE FROM ReportKpiGroupMapping WHERE KpiGroupId = @KpiGroupId",
            new { KpiGroupId = kpiGroupId });
    }
}
