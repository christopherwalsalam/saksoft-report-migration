using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using ReportConversion.Application.Interfaces;
using ReportConversion.Domain.Entities;

namespace ReportConversion.Infrastructure.Repositories;

public class KpiGroupRepository : IKpiGroupRepository
{
    private readonly string _connectionString;

    public KpiGroupRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection is not configured");
    }

    private SqlConnection CreateConnection() => new SqlConnection(_connectionString);

    public async Task<IEnumerable<KpiGroup>> GetAllAsync()
    {
        using var conn = CreateConnection();
        return await conn.QueryAsync<KpiGroup>("SELECT * FROM KpiGroups ORDER BY Name");
    }

    public async Task<KpiGroup?> GetByIdAsync(int id)
    {
        using var conn = CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<KpiGroup>(
            "SELECT * FROM KpiGroups WHERE Id = @Id", new { Id = id });
    }

    public async Task<KpiGroup?> GetByNameAsync(string name)
    {
        using var conn = CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<KpiGroup>(
            "SELECT * FROM KpiGroups WHERE Name = @Name", new { Name = name });
    }

    public async Task<int> InsertAsync(KpiGroup kpiGroup)
    {
        using var conn = CreateConnection();
        var sql = """
            INSERT INTO KpiGroups (Name, Description, Reason, CreatedAt)
            VALUES (@Name, @Description, @Reason, @CreatedAt);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """;
        return await conn.ExecuteScalarAsync<int>(sql, kpiGroup);
    }

    public async Task UpdateReasonAsync(int id, string reason)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE KpiGroups SET Reason = @Reason WHERE Id = @Id",
            new { Id = id, Reason = reason });
    }
}
