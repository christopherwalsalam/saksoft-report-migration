using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReportConversion.Application.Models;
using ReportConversion.Application.Services;
using ReportConversion.Application.Interfaces;
using ReportConversion.Domain.Enums;

namespace ReportConversion.API.Controllers;

/// <summary>
/// Manages SAP BusinessObjects metadata extraction and report querying.
/// </summary>
[ApiController]
[Route("")]
[Authorize]
[Produces("application/json")]
public class MetadataController : ControllerBase
{
    private readonly IReportRepository _reportRepository;
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly ILogger<MetadataController> _logger;

    public MetadataController(
        IReportRepository reportRepository,
        IBackgroundJobClient backgroundJobs,
        ILogger<MetadataController> logger)
    {
        _reportRepository = reportRepository;
        _backgroundJobs = backgroundJobs;
        _logger = logger;
    }

    /// <summary>
    /// Triggers a full SAP BO metadata extraction as a background job.
    /// </summary>
    /// <returns>The Hangfire job ID for tracking.</returns>
    [HttpPost("api/metadata/extract")]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status202Accepted)]
    public IActionResult TriggerExtraction([FromServices] MetadataExtractionService extractionService)
    {
        var jobId = _backgroundJobs.Enqueue(() => extractionService.ExtractAllReportsAsync(CancellationToken.None));
        _logger.LogInformation("Metadata extraction job enqueued: {JobId}", jobId);
        return Accepted(ApiResponse<string>.Ok(jobId, "Metadata extraction job started"));
    }

    /// <summary>
    /// Returns a paginated, filterable list of all extracted reports.
    /// </summary>
    /// <param name="page">Page number (default: 1)</param>
    /// <param name="pageSize">Page size (default: 50, max: 500)</param>
    /// <param name="filter">Optional name/description filter</param>
    [HttpGet("api/metadata/getreports")]
    [ProducesResponseType(typeof(PagedResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReports(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? filter = null)
    {
        pageSize = Math.Clamp(pageSize, 1, 500);
        page = Math.Max(1, page);

        var reports = await _reportRepository.GetAllAsync(page, pageSize, filter);
        var total = await _reportRepository.GetTotalCountAsync(filter);

        return Ok(new PagedResponse<object>
        {
            Success = true,
            Message = "Reports retrieved successfully",
            Data = reports,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        });
    }

    /// <summary>
    /// Returns full metadata for a single report by its internal ID.
    /// </summary>
    /// <param name="id">Internal report ID</param>
    [HttpGet("api/metadata/getreport/{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReport(int id)
    {
        var report = await _reportRepository.GetByIdAsync(id);
        if (report == null)
            return NotFound(ApiResponse<object>.Fail($"Report {id} not found"));

        return Ok(ApiResponse<object>.Ok(report));
    }

    /// <summary>
    /// Returns all stale and never-used reports based on the configured threshold.
    /// </summary>
    [HttpGet("api/metadata/getstale")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStaleReports()
    {
        var stale = await _reportRepository.GetByUsageStatusAsync(UsageStatus.Stale);
        var neverUsed = await _reportRepository.GetByUsageStatusAsync(UsageStatus.NeverUsed);

        return Ok(ApiResponse<object>.Ok(new
        {
            StaleReports = stale,
            NeverUsedReports = neverUsed,
            TotalStale = stale.Count(),
            TotalNeverUsed = neverUsed.Count()
        }));
    }
}
