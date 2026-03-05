using ReportConversion.Domain.Entities;

namespace ReportConversion.Application.Interfaces;

public interface IReportElementRepository
{
    Task<IEnumerable<ReportElement>> GetByReportIdAsync(int reportId);
    Task<int> InsertAsync(ReportElement element);
    Task DeleteByReportIdAsync(int reportId);
}
