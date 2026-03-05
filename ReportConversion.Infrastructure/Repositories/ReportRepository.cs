using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using ReportConversion.Application.Interfaces;
using ReportConversion.Domain.Entities;
using ReportConversion.Domain.Enums;

namespace ReportConversion.Infrastructure.Repositories;

public class ReportRepository : IReportRepository
{
    private readonly string _connectionString;

    public ReportRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection is not configured");
    }

    private SqlConnection CreateConnection() => new SqlConnection(_connectionString);

    public async Task<IEnumerable<Report>> GetAllAsync(int page, int pageSize, string? filter = null)
    {
        using var conn = CreateConnection();
        var offset = (page - 1) * pageSize;
        var sql = """
            SELECT r.*, kg.Name AS KpiGroupName
            FROM Reports r
            LEFT JOIN KpiGroups kg ON r.KpiGroupId = kg.Id
            WHERE (@Filter IS NULL OR r.Name LIKE '%' + @Filter + '%' OR r.Description LIKE '%' + @Filter + '%')
            ORDER BY r.Id
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
            """;
        return await conn.QueryAsync<Report>(sql, new { Filter = filter, Offset = offset, PageSize = pageSize });
    }

    public async Task<int> GetTotalCountAsync(string? filter = null)
    {
        using var conn = CreateConnection();
        var sql = """
            SELECT COUNT(*) FROM Reports
            WHERE (@Filter IS NULL OR Name LIKE '%' + @Filter + '%' OR Description LIKE '%' + @Filter + '%')
            """;
        return await conn.ExecuteScalarAsync<int>(sql, new { Filter = filter });
    }

    public async Task<Report?> GetByIdAsync(int id)
    {
        using var conn = CreateConnection();
        var sql = """
            SELECT r.*, kg.Name AS KpiGroupName
            FROM Reports r
            LEFT JOIN KpiGroups kg ON r.KpiGroupId = kg.Id
            WHERE r.Id = @Id
            """;
        return await conn.QueryFirstOrDefaultAsync<Report>(sql, new { Id = id });
    }

    public async Task<Report?> GetBySapIdAsync(string sapReportId)
    {
        using var conn = CreateConnection();
        var sql = "SELECT * FROM Reports WHERE SapReportId = @SapReportId";
        return await conn.QueryFirstOrDefaultAsync<Report>(sql, new { SapReportId = sapReportId });
    }

    public async Task<IEnumerable<Report>> GetByUsageStatusAsync(UsageStatus status)
    {
        using var conn = CreateConnection();
        var sql = """
            SELECT r.*, kg.Name AS KpiGroupName
            FROM Reports r
            LEFT JOIN KpiGroups kg ON r.KpiGroupId = kg.Id
            WHERE r.UsageStatus = @Status
            ORDER BY r.LastRunDate ASC
            """;
        return await conn.QueryAsync<Report>(sql, new { Status = status.ToString() });
    }

    public async Task<IEnumerable<Report>> GetByKpiGroupAsync(int kpiGroupId)
    {
        using var conn = CreateConnection();
        var sql = "SELECT * FROM Reports WHERE KpiGroupId = @KpiGroupId ORDER BY Name";
        return await conn.QueryAsync<Report>(sql, new { KpiGroupId = kpiGroupId });
    }

    public async Task<int> UpsertAsync(Report report)
    {
        using var conn = CreateConnection();
        var result = await conn.ExecuteScalarAsync<int>(
            "sp_UpsertReport",
            new
            {
                report.SapReportId, report.Name, report.Description, report.Owner,
                report.CreatedDate, report.LastModifiedDate, report.LastRunDate,
                ReportType = report.ReportType.ToString(),
                report.FolderPath, report.Category, report.IsScheduled,
                report.HasActiveSubscriptions,
                UsageStatus = report.UsageStatus.ToString(),
                report.ExtractedAt, report.UpdatedAt
            },
            commandType: System.Data.CommandType.StoredProcedure);
        return result;
    }

    public async Task UpdateUsageStatusAsync(int id, UsageStatus status)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE Reports SET UsageStatus = @Status, UpdatedAt = GETUTCDATE() WHERE Id = @Id",
            new { Id = id, Status = status.ToString() });
    }

    public async Task UpdateKpiGroupAsync(int id, int kpiGroupId)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE Reports SET KpiGroupId = @KpiGroupId, UpdatedAt = GETUTCDATE() WHERE Id = @Id",
            new { Id = id, KpiGroupId = kpiGroupId });
    }

    public async Task UpdateEmbeddingAsync(int id, string embeddingVector)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE Reports SET EmbeddingVector = @Vector, UpdatedAt = GETUTCDATE() WHERE Id = @Id",
            new { Id = id, Vector = embeddingVector });
    }

    public async Task<IEnumerable<Report>> GetAllForEmbeddingAsync()
    {
        using var conn = CreateConnection();
        var sql = "SELECT Id, Name, Description, EmbeddingVector FROM Reports ORDER BY Id";
        return await conn.QueryAsync<Report>(sql);
    }
}
