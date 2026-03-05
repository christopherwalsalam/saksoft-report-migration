using ReportConversion.Domain.Entities;

namespace ReportConversion.Application.Interfaces;

public interface IReportSqlRepository
{
    Task<IEnumerable<ReportSql>> GetByReportIdAsync(int reportId);
    Task<int> InsertAsync(ReportSql reportSql);
    Task DeleteByReportIdAsync(int reportId);
}
