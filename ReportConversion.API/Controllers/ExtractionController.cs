using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReportConversion.Application.Interfaces;
using ReportConversion.Application.Models;
using ReportConversion.Application.Services;

namespace ReportConversion.API.Controllers;

/// <summary>
/// UI-facing extraction endpoints — wraps MetadataExtractionService with task-tracking support.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class ExtractionController : ControllerBase
{
    private readonly IBackgroundTaskRepository _taskRepository;
    private readonly IReportRepository _reportRepository;
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly ILogger<ExtractionController> _logger;

    public ExtractionController(
        IBackgroundTaskRepository taskRepository,
        IReportRepository reportRepository,
        IBackgroundJobClient backgroundJobs,
        ILogger<ExtractionController> logger)
    {
        _taskRepository = taskRepository;
        _reportRepository = reportRepository;
        _backgroundJobs = backgroundJobs;
        _logger = logger;
    }

    /// <summary>
    /// Triggers SAP BO metadata extraction. Returns a taskId for polling progress.
    /// </summary>
    [HttpPost("start")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> StartExtraction([FromServices] MetadataExtractionService extractionService)
    {
        var taskId = await _taskRepository.CreateAsync("Extraction");
        _backgroundJobs.Enqueue(() => extractionService.ExtractAllReportsWithTrackingAsync(taskId, CancellationToken.None));
        _logger.LogInformation("Extraction task started: {TaskId}", taskId);
        return Accepted(ApiResponse<object>.Ok(new { taskId }, "Extraction started"));
    }

    /// <summary>
    /// Returns a paginated, filterable list of extracted reports.
    /// </summary>
    [HttpGet("/api/reports")]
    [ProducesResponseType(typeof(PagedResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReports(
        [FromQuery] string? search = null,
        [FromQuery] string? type = null,
        [FromQuery] string? folder = null,
        [FromQuery] string? fromDate = null,
        [FromQuery] string? toDate = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);
        page = Math.Max(1, page);

        DateTime? from = string.IsNullOrWhiteSpace(fromDate) ? null : DateTime.TryParse(fromDate, out var fd) ? fd : null;
        DateTime? to = string.IsNullOrWhiteSpace(toDate) ? null : DateTime.TryParse(toDate, out var td) ? td : null;

        var reports = await _reportRepository.GetAllFilteredAsync(search, type, folder, from, to, page, pageSize);
        var total = await _reportRepository.GetFilteredCountAsync(search, type, folder, from, to);

        return Ok(new PagedResponse<object>
        {
            Success = true,
            Message = "Reports retrieved",
            Data = reports,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        });
    }

    /// <summary>
    /// Returns full metadata and SQL for a single report.
    /// </summary>
    [HttpGet("/api/reports/{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReport(int id, [FromServices] IReportSqlRepository sqlRepository)
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
            UsageStatus = report.UsageStatus.ToString(),
            report.KpiGroupName,
            report.Description,
            report.Owner,
            SqlQueries = sqls.Select(s => s.SqlText).Where(q => !string.IsNullOrWhiteSpace(q)).ToList()
        }));
    }
}
