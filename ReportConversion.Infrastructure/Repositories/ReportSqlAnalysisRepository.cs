using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using ReportConversion.Application.Interfaces;
using ReportConversion.Domain.Entities;

namespace ReportConversion.Infrastructure.Repositories;

public class ReportSqlAnalysisRepository : IReportSqlAnalysisRepository
{
    private readonly string _connectionString;

    public ReportSqlAnalysisRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection is not configured");
    }

    private SqlConnection CreateConnection() => new SqlConnection(_connectionString);

    public async Task<ReportSqlAnalysis?> GetByReportIdAsync(int reportId)
    {
        using var conn = CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<ReportSqlAnalysis>(
            "SELECT * FROM ReportSqlAnalysis WHERE ReportId = @ReportId",
            new { ReportId = reportId });
    }

    public async Task<IEnumerable<ReportSqlAnalysis>> GetAllAsync()
    {
        using var conn = CreateConnection();
        return await conn.QueryAsync<ReportSqlAnalysis>("SELECT * FROM ReportSqlAnalysis ORDER BY ReportId");
    }

    public async Task<int> UpsertAsync(ReportSqlAnalysis analysis)
    {
        using var conn = CreateConnection();
        var sql = """
            IF EXISTS (SELECT 1 FROM ReportSqlAnalysis WHERE ReportId = @ReportId)
            BEGIN
                UPDATE ReportSqlAnalysis
                SET TablesReferenced    = @TablesReferenced,
                    ColumnsUsed         = @ColumnsUsed,
                    AggregationPatterns = @AggregationPatterns,
                    JoinPatterns        = @JoinPatterns,
                    FilterPatterns      = @FilterPatterns,
                    SqlSummary          = @SqlSummary,
                    AnalysedAt          = @AnalysedAt
                WHERE ReportId = @ReportId;
                SELECT Id FROM ReportSqlAnalysis WHERE ReportId = @ReportId;
            END
            ELSE
            BEGIN
                INSERT INTO ReportSqlAnalysis
                    (ReportId, TablesReferenced, ColumnsUsed, AggregationPatterns, JoinPatterns, FilterPatterns, SqlSummary, AnalysedAt)
                VALUES
                    (@ReportId, @TablesReferenced, @ColumnsUsed, @AggregationPatterns, @JoinPatterns, @FilterPatterns, @SqlSummary, @AnalysedAt);
                SELECT CAST(SCOPE_IDENTITY() AS INT);
            END
            """;
        return await conn.ExecuteScalarAsync<int>(sql, analysis);
    }
}
