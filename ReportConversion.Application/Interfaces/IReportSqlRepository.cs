using ReportConversion.Domain.Entities;

namespace ReportConversion.Application.Interfaces;

public interface IReportSqlRepository
{
    Task<IEnumerable<ReportSql>> GetByReportIdAsync(int reportId);

    /// <summary>Returns all SQL records for all reports in a single query.
    /// Used by duplicate detection to avoid N+1 per-report calls.</summary>
    Task<IEnumerable<ReportSql>> GetAllAsync();

    Task<int> InsertAsync(ReportSql reportSql);
    Task DeleteByReportIdAsync(int reportId);
}
