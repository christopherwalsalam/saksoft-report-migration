using ReportConversion.Domain.Entities;

namespace ReportConversion.Application.Interfaces;

public interface IBackgroundTaskRepository
{
    Task<BackgroundTask?> GetByTaskIdAsync(string taskId);
    Task<string> CreateAsync(string taskType);
    Task UpdateProgressAsync(string taskId, string status, int progressPercent, string? currentStep = null);
    Task CompleteAsync(string taskId);
    Task FailAsync(string taskId, string errorMessage);
}
