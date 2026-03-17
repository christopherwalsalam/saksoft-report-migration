using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using ReportConversion.Application.Interfaces;
using ReportConversion.Domain.Entities;
using ReportConversion.Domain.Enums;

namespace ReportConversion.Infrastructure.Repositories;

public class DuplicateReportRepository : IDuplicateReportRepository
{
    private readonly string _connectionString;

    public DuplicateReportRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection is not configured");
    }

    private SqlConnection CreateConnection() => new SqlConnection(_connectionString);

    public async Task<IEnumerable<DuplicateReport>> GetAllAsync(double? minSimilarity = null)
    {
        using var conn = CreateConnection();
        var sql = """
            SELECT d.*, r1.Name AS Report1Name, r2.Name AS Report2Name
            FROM DuplicateReports d
            JOIN Reports r1 ON d.ReportId1 = r1.Id
            JOIN Reports r2 ON d.ReportId2 = r2.Id
            WHERE (@MinSimilarity IS NULL OR d.SimilarityScore >= @MinSimilarity)
            ORDER BY d.SimilarityScore DESC
            """;
        return await conn.QueryAsync<DuplicateReport>(sql, new { MinSimilarity = minSimilarity });
    }

    public async Task<int> InsertAsync(DuplicateReport duplicate)
    {
        using var conn = CreateConnection();
        var sql = """
            INSERT INTO DuplicateReports (ReportId1, ReportId2, SimilarityScore, RecommendedAction, DetectionMethod, DetectedAt)
            VALUES (@ReportId1, @ReportId2, @SimilarityScore, @RecommendedAction, @DetectionMethod, @DetectedAt);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """;
        return await conn.ExecuteScalarAsync<int>(sql, new
        {
            duplicate.ReportId1,
            duplicate.ReportId2,
            duplicate.SimilarityScore,
            RecommendedAction = duplicate.RecommendedAction.ToString(),
            DetectionMethod   = duplicate.DetectionMethod.ToString(),
            duplicate.DetectedAt
        });
    }

    public async Task<bool> PairExistsAsync(int reportId1, int reportId2)
    {
        using var conn = CreateConnection();
        var count = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM DuplicateReports WHERE (ReportId1=@Id1 AND ReportId2=@Id2) OR (ReportId1=@Id2 AND ReportId2=@Id1)",
            new { Id1 = reportId1, Id2 = reportId2 });
        return count > 0;
    }

    public async Task DeleteAllAsync()
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync("DELETE FROM DuplicateReports");
    }
}
