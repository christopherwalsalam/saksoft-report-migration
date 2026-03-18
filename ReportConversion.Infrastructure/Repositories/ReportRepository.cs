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

    public async Task<IEnumerable<Report>> GetAllFilteredAsync(
        string? search, string? type, string? folder,
        DateTime? fromDate, DateTime? toDate,
        int page, int pageSize)
    {
        using var conn = CreateConnection();
        var offset = (page - 1) * pageSize;
        var sql = """
            SELECT r.*, kg.Name AS KpiGroupName
            FROM Reports r
            LEFT JOIN KpiGroups kg ON r.KpiGroupId = kg.Id
            WHERE (@Search IS NULL OR r.Name LIKE '%' + @Search + '%')
              AND (@Type IS NULL OR r.ReportType = @Type)
              AND (@Folder IS NULL OR r.FolderPath LIKE '%' + @Folder + '%')
              AND (@FromDate IS NULL OR r.CreatedDate >= @FromDate)
              AND (@ToDate IS NULL OR r.CreatedDate <= @ToDate)
            ORDER BY r.Name
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
            """;
        return await conn.QueryAsync<Report>(sql, new
        {
            Search = search, Type = type, Folder = folder,
            FromDate = fromDate, ToDate = toDate,
            Offset = offset, PageSize = pageSize
        });
    }

    public async Task<int> GetFilteredCountAsync(
        string? search, string? type, string? folder,
        DateTime? fromDate, DateTime? toDate)
    {
        using var conn = CreateConnection();
        var sql = """
            SELECT COUNT(*) FROM Reports
            WHERE (@Search IS NULL OR Name LIKE '%' + @Search + '%')
              AND (@Type IS NULL OR ReportType = @Type)
              AND (@Folder IS NULL OR FolderPath LIKE '%' + @Folder + '%')
              AND (@FromDate IS NULL OR CreatedDate >= @FromDate)
              AND (@ToDate IS NULL OR CreatedDate <= @ToDate)
            """;
        return await conn.ExecuteScalarAsync<int>(sql, new
        {
            Search = search, Type = type, Folder = folder,
            FromDate = fromDate, ToDate = toDate
        });
    }

    public async Task<IEnumerable<Report>> GetStaleFilteredAsync(
        string? search, UsageStatus? status,
        DateTime? fromDate, DateTime? toDate,
        int page, int pageSize)
    {
        using var conn = CreateConnection();
        var offset = (page - 1) * pageSize;
        var statusFilter = status.HasValue ? status.Value.ToString() : null;

        // Default: return NeverUsed and Stale when no explicit filter
        var sql = """
            SELECT r.*, kg.Name AS KpiGroupName
            FROM Reports r
            LEFT JOIN KpiGroups kg ON r.KpiGroupId = kg.Id
            WHERE r.UsageStatus IN ('NeverUsed', 'Stale')
              AND (@Status IS NULL OR r.UsageStatus = @Status)
              AND (@Search IS NULL OR r.Name LIKE '%' + @Search + '%')
              AND (@FromDate IS NULL OR r.LastRunDate >= @FromDate)
              AND (@ToDate IS NULL OR r.LastRunDate <= @ToDate)
            ORDER BY r.LastRunDate ASC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
            """;
        return await conn.QueryAsync<Report>(sql, new
        {
            Status = statusFilter, Search = search,
            FromDate = fromDate, ToDate = toDate,
            Offset = offset, PageSize = pageSize
        });
    }

    public async Task<int> GetStaleFilteredCountAsync(
        string? search, UsageStatus? status,
        DateTime? fromDate, DateTime? toDate)
    {
        using var conn = CreateConnection();
        var statusFilter = status.HasValue ? status.Value.ToString() : null;
        var sql = """
            SELECT COUNT(*) FROM Reports
            WHERE UsageStatus IN ('NeverUsed', 'Stale')
              AND (@Status IS NULL OR UsageStatus = @Status)
              AND (@Search IS NULL OR Name LIKE '%' + @Search + '%')
              AND (@FromDate IS NULL OR LastRunDate >= @FromDate)
              AND (@ToDate IS NULL OR LastRunDate <= @ToDate)
            """;
        return await conn.ExecuteScalarAsync<int>(sql, new
        {
            Status = statusFilter, Search = search,
            FromDate = fromDate, ToDate = toDate
        });
    }
}
