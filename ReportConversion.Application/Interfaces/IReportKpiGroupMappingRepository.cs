using ReportConversion.Domain.Entities;

namespace ReportConversion.Application.Interfaces;

public interface IReportKpiGroupMappingRepository
{
    /// <summary>Returns all reports assigned to the given KPI group.</summary>
    Task<IEnumerable<ReportKpiGroupMapping>> GetByKpiGroupIdAsync(int kpiGroupId);

    /// <summary>Returns the KPI group assignment for a specific report.</summary>
    Task<ReportKpiGroupMapping?> GetByReportIdAsync(int reportId);

    /// <summary>Returns all mapping records (full report-to-group assignment table).</summary>
    Task<IEnumerable<ReportKpiGroupMapping>> GetAllAsync();

    /// <summary>Inserts a new mapping. Ignores duplicates (idempotent).</summary>
    Task<int> InsertAsync(ReportKpiGroupMapping mapping);

    /// <summary>Removes all mappings for a report (used before re-assigning).</summary>
    Task DeleteByReportIdAsync(int reportId);

    /// <summary>Removes all mappings for a KPI group.</summary>
    Task DeleteByKpiGroupIdAsync(int kpiGroupId);
}
