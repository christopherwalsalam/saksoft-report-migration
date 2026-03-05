using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using ReportConversion.Application.Interfaces;
using ReportConversion.Domain.Entities;

namespace ReportConversion.Infrastructure.Repositories;

public class ReportElementRepository : IReportElementRepository
{
    private readonly string _connectionString;

    public ReportElementRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection is not configured");
    }

    private SqlConnection CreateConnection() => new SqlConnection(_connectionString);

    public async Task<IEnumerable<ReportElement>> GetByReportIdAsync(int reportId)
    {
        using var conn = CreateConnection();
        return await conn.QueryAsync<ReportElement>(
            "SELECT * FROM ReportElements WHERE ReportId = @ReportId",
            new { ReportId = reportId });
    }

    public async Task<int> InsertAsync(ReportElement element)
    {
        using var conn = CreateConnection();
        var sql = """
            INSERT INTO ReportElements (ReportId, ElementType, ElementName, Properties, PowerBiEquivalent)
            VALUES (@ReportId, @ElementType, @ElementName, @Properties, @PowerBiEquivalent);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """;
        return await conn.ExecuteScalarAsync<int>(sql, element);
    }

    public async Task DeleteByReportIdAsync(int reportId)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync("DELETE FROM ReportElements WHERE ReportId = @ReportId", new { ReportId = reportId });
    }
}
