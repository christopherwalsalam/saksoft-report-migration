using ReportConversion.Domain.Entities;
using ReportConversion.Domain.Enums;

namespace ReportConversion.Application.Interfaces;

public interface IReportRepository
{
    Task<IEnumerable<Report>> GetAllAsync(int page, int pageSize, string? filter = null);
    Task<int> GetTotalCountAsync(string? filter = null);
    Task<Report?> GetByIdAsync(int id);
    Task<Report?> GetBySapIdAsync(string sapReportId);
    Task<IEnumerable<Report>> GetByUsageStatusAsync(UsageStatus status);
    Task<IEnumerable<Report>> GetByKpiGroupAsync(int kpiGroupId);
    Task<int> UpsertAsync(Report report);
    Task UpdateUsageStatusAsync(int id, UsageStatus status);
    Task UpdateKpiGroupAsync(int id, int kpiGroupId);
    Task UpdateEmbeddingAsync(int id, string embeddingVector);
    Task<IEnumerable<Report>> GetAllForEmbeddingAsync();

    // UI-facing filtered queries
    Task<IEnumerable<Report>> GetAllFilteredAsync(string? search, string? type, string? folder, DateTime? fromDate, DateTime? toDate, int page, int pageSize);
    Task<int> GetFilteredCountAsync(string? search, string? type, string? folder, DateTime? fromDate, DateTime? toDate);
    Task<IEnumerable<Report>> GetStaleFilteredAsync(string? search, UsageStatus? status, DateTime? fromDate, DateTime? toDate, int page, int pageSize);
    Task<int> GetStaleFilteredCountAsync(string? search, UsageStatus? status, DateTime? fromDate, DateTime? toDate);
}
