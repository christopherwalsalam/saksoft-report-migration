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
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly ILogger<MigrationController> _logger;

    public MigrationController(
        IMigrationJobRepository jobRepository,
        IMigrationLogRepository logRepository,
        IBackgroundJobClient backgroundJobs,
        ILogger<MigrationController> logger)
    {
        _jobRepository = jobRepository;
        _logRepository = logRepository;
        _backgroundJobs = backgroundJobs;
        _logger = logger;
    }

    /// <summary>
    /// Starts migration for all reports, or a specific set of report IDs.
    /// </summary>
    /// <param name="request">Optional list of report IDs to migrate. Leave empty to migrate all.</param>
    [HttpPost("start")]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status202Accepted)]
    public IActionResult StartMigration(
        [FromBody] MigrationStartRequest? request,
        [FromServices] MigrationService migrationService)
    {
        string jobId;
        if (request?.ReportIds != null && request.ReportIds.Any())
        {
            var ids = request.ReportIds.ToList();
            jobId = _backgroundJobs.Enqueue(() => migrationService.MigrateReportsByIdsAsync(ids, CancellationToken.None));
            _logger.LogInformation("Migration job enqueued for {Count} reports: {JobId}", ids.Count, jobId);
            return Accepted(ApiResponse<string>.Ok(jobId, $"Migration started for {ids.Count} reports"));
        }
        else
        {
            jobId = _backgroundJobs.Enqueue(() => migrationService.MigrateAllReportsAsync(CancellationToken.None));
            _logger.LogInformation("Full migration job enqueued: {JobId}", jobId);
            return Accepted(ApiResponse<string>.Ok(jobId, "Full migration started for all reports"));
        }
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
