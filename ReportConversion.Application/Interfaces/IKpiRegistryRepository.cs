using ReportConversion.Domain.Entities;

namespace ReportConversion.Application.Interfaces;

public interface IKpiRegistryRepository
{
    Task<IEnumerable<KpiRegistry>> GetAllAsync();
    Task<IEnumerable<KpiRegistry>> GetByGroupIdAsync(int groupId);
    Task<int> InsertAsync(KpiRegistry kpiRegistry);
    Task<bool> ExistsAsync(string kpiName, int kpiGroupId);
}
