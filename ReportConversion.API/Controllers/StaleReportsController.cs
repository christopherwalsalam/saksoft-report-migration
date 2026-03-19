using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReportConversion.Application.Interfaces;
using ReportConversion.Application.Models;
using ReportConversion.Application.Services;
using ReportConversion.Domain.Enums;

namespace ReportConversion.API.Controllers;

/// <summary>
/// UI-facing stale report endpoints — triggers analysis and returns paginated stale/unused reports.
/// </summary>
[ApiController]
[Route("")]
[Authorize]
[Produces("application/json")]
public class StaleReportsController : ControllerBase
{
    private readonly IBackgroundTaskRepository _taskRepository;
    private readonly IReportRepository _reportRepository;
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly ILogger<StaleReportsController> _logger;

    public StaleReportsController(
        IBackgroundTaskRepository taskRepository,
        IReportRepository reportRepository,
        IBackgroundJobClient backgroundJobs,
        ILogger<StaleReportsController> logger)
    {
        _taskRepository = taskRepository;
        _reportRepository = reportRepository;
        _backgroundJobs = backgroundJobs;
        _logger = logger;
    }

    /// <summary>
    /// Triggers stale report analysis. Returns a taskId for polling.
    /// </summary>
    [HttpPost("api/stale-reports/start")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> StartStaleAnalysis([FromServices] StaleReportService staleReportService)
    {
        var taskId = await _taskRepository.CreateAsync("StaleReport");
        _backgroundJobs.Enqueue(() => staleReportService.IdentifyStaleReportsWithTrackingAsync(taskId, CancellationToken.None));
        _logger.LogInformation("Stale report analysis task started: {TaskId}", taskId);
        return Accepted(ApiResponse<object>.Ok(new { taskId }, "Stale report analysis started"));
    }

    /// <summary>
    /// Returns paginated stale and never-used reports with optional filters.
    /// </summary>
    [HttpGet("api/stale-reports/getstale")]
    [ProducesResponseType(typeof(PagedResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStaleReports(
        [FromQuery] string? search = null,
        [FromQuery] string? staleReason = null,
        [FromQuery] string? fromDate = null,
        [FromQuery] string? toDate = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);
        page = Math.Max(1, page);

        DateTime? from = string.IsNullOrWhiteSpace(fromDate) ? null : DateTime.TryParse(fromDate, out var fd) ? fd : null;
        DateTime? to = string.IsNullOrWhiteSpace(toDate) ? null : DateTime.TryParse(toDate, out var td) ? td : null;

        // Map staleReason to UsageStatus filter
        UsageStatus? statusFilter = staleReason switch
        {
            "NeverUsed" => UsageStatus.NeverUsed,
            "Stale"     => UsageStatus.Stale,
            _           => null
        };

        var reports = await _reportRepository.GetStaleFilteredAsync(search, statusFilter, from, to, page, pageSize);
        var total = await _reportRepository.GetStaleFilteredCountAsync(search, statusFilter, from, to);

        return Ok(new PagedResponse<object>
        {
            Success = true,
            Message = "Stale reports retrieved",
            Data = reports,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        });
    }

    /// <summary>
    /// Returns full metadata for a single stale report.
    /// </summary>
    [HttpGet("api/stale-reports/getstale/{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStaleReport(int id, [FromServices] IReportSqlRepository sqlRepository)
    {
        var report = await _reportRepository.GetByIdAsync(id);
        if (report == null)
            return NotFound(ApiResponse<object>.Fail($"Report {id} not found"));

        var sqls = await sqlRepository.GetByReportIdAsync(id);

        return Ok(ApiResponse<object>.Ok(new
        {
            report.Id,
            report.Name,
            Type = report.ReportType.ToString(),
            report.FolderPath,
            report.CreatedDate,
            report.LastRunDate,
            report.IsScheduled,
            report.HasActiveSubscriptions,
            StaleReason = report.UsageStatus.ToString(),
            UsageStatus = report.UsageStatus.ToString(),
            report.KpiGroupName,
            report.Description,
            report.Owner,
            SqlQueries = sqls.Select(s => s.SqlText).Where(q => !string.IsNullOrWhiteSpace(q)).ToList()
        }));
    }
}
