using ReportConversion.Domain.Entities;

namespace ReportConversion.Application.Interfaces;

public interface IKpiGroupRepository
{
    Task<IEnumerable<KpiGroup>> GetAllAsync();
    Task<KpiGroup?> GetByIdAsync(int id);
    Task<KpiGroup?> GetByNameAsync(string name);
    Task<int> InsertAsync(KpiGroup kpiGroup);
}
