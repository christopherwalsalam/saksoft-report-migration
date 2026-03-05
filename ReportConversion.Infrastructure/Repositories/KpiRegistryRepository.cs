using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using ReportConversion.Application.Interfaces;
using ReportConversion.Domain.Entities;

namespace ReportConversion.Infrastructure.Repositories;

public class KpiRegistryRepository : IKpiRegistryRepository
{
    private readonly string _connectionString;

    public KpiRegistryRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection is not configured");
    }

    private SqlConnection CreateConnection() => new SqlConnection(_connectionString);

    public async Task<IEnumerable<KpiRegistry>> GetAllAsync()
    {
        using var conn = CreateConnection();
        var sql = """
            SELECT kr.*, kg.Name AS BusinessDomain
            FROM KpiRegistry kr
            JOIN KpiGroups kg ON kr.KpiGroupId = kg.Id
            ORDER BY kg.Name, kr.KpiName
            """;
        return await conn.QueryAsync<KpiRegistry>(sql);
    }

    public async Task<IEnumerable<KpiRegistry>> GetByGroupIdAsync(int groupId)
    {
        using var conn = CreateConnection();
        return await conn.QueryAsync<KpiRegistry>(
            "SELECT * FROM KpiRegistry WHERE KpiGroupId = @GroupId ORDER BY KpiName",
            new { GroupId = groupId });
    }

    public async Task<int> InsertAsync(KpiRegistry kpiRegistry)
    {
        using var conn = CreateConnection();
        var sql = """
            INSERT INTO KpiRegistry (KpiName, KpiGroupId, BusinessDomain, MetricsInvolved, ReportCount, DataSourcesInvolved, CreatedAt)
            VALUES (@KpiName, @KpiGroupId, @BusinessDomain, @MetricsInvolved, @ReportCount, @DataSourcesInvolved, @CreatedAt);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """;
        return await conn.ExecuteScalarAsync<int>(sql, kpiRegistry);
    }

    public async Task<bool> ExistsAsync(string kpiName, int kpiGroupId)
    {
        using var conn = CreateConnection();
        var count = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM KpiRegistry WHERE KpiName = @KpiName AND KpiGroupId = @KpiGroupId",
            new { KpiName = kpiName, KpiGroupId = kpiGroupId });
        return count > 0;
    }
}
