using ReportConversion.Domain.Entities;

namespace ReportConversion.Application.Interfaces;

public interface IReportSqlAnalysisRepository
{
    Task<ReportSqlAnalysis?> GetByReportIdAsync(int reportId);
    Task<IEnumerable<ReportSqlAnalysis>> GetAllAsync();
    Task<int> UpsertAsync(ReportSqlAnalysis analysis);
}
