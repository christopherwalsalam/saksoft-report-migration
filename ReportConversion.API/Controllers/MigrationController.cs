using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReportConversion.Application.Interfaces;
using ReportConversion.Application.Models;
using ReportConversion.Application.Services;

namespace ReportConversion.API.Controllers;

/// <summary>
/// Controls Power BI migration operations — starting jobs, tracking progress, and reviewing logs.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class MigrationController : ControllerBase
{
    private readonly IMigrationJobRepository _jobRepository;
    private readonly IMigrationLogRepository _logRepository;
    private readonly IBackgroundTaskRepository _taskRepository;
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly ILogger<MigrationController> _logger;

    public MigrationController(
        IMigrationJobRepository jobRepository,
        IMigrationLogRepository logRepository,
        IBackgroundTaskRepository taskRepository,
        IBackgroundJobClient backgroundJobs,
        ILogger<MigrationController> logger)
    {
        _jobRepository = jobRepository;
        _logRepository = logRepository;
        _taskRepository = taskRepository;
        _backgroundJobs = backgroundJobs;
        _logger = logger;
    }

    /// <summary>
    /// Starts migration for all reports, or a specific set of report IDs.
    /// Returns a taskId for UI polling in addition to the Hangfire job ID.
    /// </summary>
    /// <param name="request">Optional list of report IDs to migrate. Leave empty to migrate all.</param>
    [HttpPost("start")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> StartMigration(
        [FromBody] MigrationStartRequest? request,
        [FromServices] MigrationService migrationService)
    {
        var taskId = await _taskRepository.CreateAsync("Migration");

        if (request?.ReportIds != null && request.ReportIds.Any())
        {
            var ids = request.ReportIds.ToList();
            _backgroundJobs.Enqueue(() => migrationService.MigrateReportsByIdsWithTrackingAsync(ids, taskId, CancellationToken.None));
            _logger.LogInformation("Migration task started for {Count} reports: {TaskId}", ids.Count, taskId);
            return Accepted(ApiResponse<object>.Ok(new { taskId }, $"Migration started for {ids.Count} reports"));
        }
        else
        {
            _backgroundJobs.Enqueue(() => migrationService.MigrateAllReportsWithTrackingAsync(taskId, CancellationToken.None));
            _logger.LogInformation("Full migration task started: {TaskId}", taskId);
            return Accepted(ApiResponse<object>.Ok(new { taskId }, "Full migration started for all reports"));
        }
    }

    /// <summary>
    /// Returns a migration summary — called by the UI after the migration task completes.
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(ApiResponse<MigrationSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMigrationSummary()
    {
        var (total, success, failed, pending, inProgress) = await _jobRepository.GetSummaryAsync();

        var summary = new MigrationSummaryDto
        {
            TotalAttempted = total,
            SuccessCount = success,
            FailureCount = failed,
            SuccessRate = total > 0 ? Math.Round((double)success / total * 100, 1) : 0.0,
        };

        return Ok(ApiResponse<MigrationSummaryDto>.Ok(summary));
    }

    /// <summary>
    /// Returns an overall summary of migration progress across all reports.
    /// </summary>
    [HttpGet("status")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMigrationStatus()
    {
        var (total, success, failed, pending, inProgress) = await _jobRepository.GetSummaryAsync();
        return Ok(ApiResponse<object>.Ok(new
        {
            Total = total,
            Success = success,
            Failed = failed,
            Pending = pending,
            InProgress = inProgress,
            SuccessRate = total > 0 ? Math.Round((double)success / total * 100, 1) : 0.0
        }));
    }

    /// <summary>
    /// Returns the status of a specific migration job by its job ID.
    /// </summary>
    /// <param name="jobId">Internal migration job ID</param>
    [HttpGet("status/{jobId:int}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetJobStatus(int jobId)
    {
        var job = await _jobRepository.GetByIdAsync(jobId);
        if (job == null)
            return NotFound(ApiResponse<object>.Fail($"Migration job {jobId} not found"));

        return Ok(ApiResponse<object>.Ok(job));
    }

    /// <summary>
    /// Returns all migration log entries for a specific report.
    /// </summary>
    /// <param name="reportId">Internal report ID</param>
    [HttpGet("logs/{reportId:int}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMigrationLogs(int reportId)
    {
        var logs = await _logRepository.GetByReportIdAsync(reportId);
        return Ok(ApiResponse<object>.Ok(logs));
    }
}

/// <summary>
/// Request body for starting a migration.
/// </summary>
public class MigrationStartRequest
{
    /// <summary>
    /// Specific report IDs to migrate. If null or empty, all reports are migrated.
    /// </summary>
    public IEnumerable<int>? ReportIds { get; set; }
}
