using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using ReportConversion.Application.Interfaces;
using ReportConversion.Domain.Entities;

namespace ReportConversion.Infrastructure.Repositories;

public class BackgroundTaskRepository : IBackgroundTaskRepository
{
    private readonly string _connectionString;

    public BackgroundTaskRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection is not configured");
    }

    private SqlConnection CreateConnection() => new SqlConnection(_connectionString);

    public async Task<BackgroundTask?> GetByTaskIdAsync(string taskId)
    {
        using var conn = CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<BackgroundTask>(
            "SELECT * FROM BackgroundTasks WHERE TaskId = @TaskId",
            new { TaskId = taskId });
    }

    public async Task<string> CreateAsync(string taskType)
    {
        var taskId = Guid.NewGuid().ToString("N")[..16];
        using var conn = CreateConnection();
        await conn.ExecuteAsync(
            """
            INSERT INTO BackgroundTasks (TaskId, TaskType, Status, ProgressPercent, StartedAt)
            VALUES (@TaskId, @TaskType, 'Pending', 0, @StartedAt)
            """,
            new { TaskId = taskId, TaskType = taskType, StartedAt = DateTime.UtcNow });
        return taskId;
    }

    public async Task UpdateProgressAsync(string taskId, string status, int progressPercent, string? currentStep = null)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync(
            """
            UPDATE BackgroundTasks
            SET Status = @Status,
                ProgressPercent = @ProgressPercent,
                CurrentStep = @CurrentStep
            WHERE TaskId = @TaskId
            """,
            new { TaskId = taskId, Status = status, ProgressPercent = progressPercent, CurrentStep = currentStep });
    }

    public async Task CompleteAsync(string taskId)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync(
            """
            UPDATE BackgroundTasks
            SET Status = 'Completed',
                ProgressPercent = 100,
                CurrentStep = 'Completed',
                CompletedAt = @CompletedAt
            WHERE TaskId = @TaskId
            """,
            new { TaskId = taskId, CompletedAt = DateTime.UtcNow });
    }

    public async Task FailAsync(string taskId, string errorMessage)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync(
            """
            UPDATE BackgroundTasks
            SET Status = 'Failed',
                ErrorMessage = @ErrorMessage,
                CompletedAt = @CompletedAt
            WHERE TaskId = @TaskId
            """,
            new { TaskId = taskId, ErrorMessage = errorMessage, CompletedAt = DateTime.UtcNow });
    }
}
