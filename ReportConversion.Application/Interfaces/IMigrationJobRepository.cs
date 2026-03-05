using ReportConversion.Domain.Entities;
using ReportConversion.Domain.Enums;

namespace ReportConversion.Application.Interfaces;

public interface IMigrationJobRepository
{
    Task<IEnumerable<MigrationJob>> GetAllAsync();
    Task<MigrationJob?> GetByIdAsync(int id);
    Task<MigrationJob?> GetByReportIdAsync(int reportId);
    Task<int> InsertAsync(MigrationJob job);
    Task UpdateStatusAsync(int id, MigrationStatus status, string? errorMessage = null, string? outputPath = null, string? blobPath = null);
    Task<(int Total, int Success, int Failed, int Pending, int InProgress)> GetSummaryAsync();
}
