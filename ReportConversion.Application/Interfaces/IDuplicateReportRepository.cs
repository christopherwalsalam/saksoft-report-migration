using ReportConversion.Domain.Entities;

namespace ReportConversion.Application.Interfaces;

public interface IDuplicateReportRepository
{
    Task<IEnumerable<DuplicateReport>> GetAllAsync(double? minSimilarity = null);
    Task<int> InsertAsync(DuplicateReport duplicate);
    Task<bool> PairExistsAsync(int reportId1, int reportId2);
    Task DeleteAllAsync();
}
