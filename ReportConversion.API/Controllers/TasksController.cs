using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReportConversion.Application.Interfaces;
using ReportConversion.Application.Models;

namespace ReportConversion.API.Controllers;

/// <summary>
/// Provides task status polling for long-running background operations.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class TasksController : ControllerBase
{
    private readonly IBackgroundTaskRepository _taskRepository;
    private readonly ILogger<TasksController> _logger;

    public TasksController(IBackgroundTaskRepository taskRepository, ILogger<TasksController> logger)
    {
        _taskRepository = taskRepository;
        _logger = logger;
    }

    /// <summary>
    /// Returns the current status and progress of a background task.
    /// Polled every 3 seconds by the React UI.
    /// </summary>
    /// <param name="taskId">The task ID returned when the job was triggered.</param>
    [HttpGet("{taskId}/status")]
    [ProducesResponseType(typeof(ApiResponse<TaskStatusDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTaskStatus(string taskId)
    {
        var task = await _taskRepository.GetByTaskIdAsync(taskId);
        if (task == null)
            return NotFound(ApiResponse<TaskStatusDto>.Fail($"Task '{taskId}' not found"));

        var dto = new TaskStatusDto
        {
            TaskId = task.TaskId,
            TaskType = task.TaskType,
            Status = task.Status,
            ProgressPercent = task.ProgressPercent,
            CurrentStep = task.CurrentStep,
            StartedAt = task.StartedAt,
            CompletedAt = task.CompletedAt,
            ErrorMessage = task.ErrorMessage,
        };

        return Ok(ApiResponse<TaskStatusDto>.Ok(dto));
    }
}
