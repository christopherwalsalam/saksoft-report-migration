using ReportConversion.Domain.Entities;

namespace ReportConversion.Application.Interfaces;

public interface IMigrationLogRepository
{
    Task<IEnumerable<MigrationLog>> GetByReportIdAsync(int reportId);
    Task<IEnumerable<MigrationLog>> GetByJobIdAsync(int jobId);
    Task<int> InsertAsync(MigrationLog log);
}
