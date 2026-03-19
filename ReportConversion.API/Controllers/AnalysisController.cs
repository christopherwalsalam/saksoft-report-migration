using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReportConversion.Application.Interfaces;
using ReportConversion.Application.Models;
using ReportConversion.Application.Services;

namespace ReportConversion.API.Controllers;

/// <summary>
/// Handles AI-powered KPI grouping, KPI registry, and duplicate detection analysis.
/// </summary>
[ApiController]
[Route("")]
[Authorize]
[Produces("application/json")]
public class AnalysisController : ControllerBase
{
    private readonly IKpiGroupRepository _kpiGroupRepository;
    private readonly IKpiRegistryRepository _kpiRegistryRepository;
    private readonly IDuplicateReportRepository _duplicateRepository;
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly ILogger<AnalysisController> _logger;

    public AnalysisController(
        IKpiGroupRepository kpiGroupRepository,
        IKpiRegistryRepository kpiRegistryRepository,
        IDuplicateReportRepository duplicateRepository,
        IBackgroundJobClient backgroundJobs,
        ILogger<AnalysisController> logger)
    {
        _kpiGroupRepository = kpiGroupRepository;
        _kpiRegistryRepository = kpiRegistryRepository;
        _duplicateRepository = duplicateRepository;
        _backgroundJobs = backgroundJobs;
        _logger = logger;
    }

    /// <summary>
    /// Triggers KPI grouping via Azure OpenAI (GPT-4o) as a background job.
    /// </summary>
    [HttpPost("api/analysis/groupbykpi")]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status202Accepted)]
    public IActionResult TriggerKpiGrouping([FromServices] KpiGroupingService kpiGroupingService)
    {
        var jobId = _backgroundJobs.Enqueue(() => kpiGroupingService.GroupReportsByKpiAsync(CancellationToken.None));
        _logger.LogInformation("KPI grouping job enqueued: {JobId}", jobId);
        return Accepted(ApiResponse<string>.Ok(jobId, "KPI grouping job started"));
    }

    /// <summary>
    /// Returns all KPI groups with their associated report counts.
    /// </summary>
    [HttpGet("api/analysis/getkpigroups")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetKpiGroups([FromServices] IReportRepository reportRepository)
    {
        var groups = await _kpiGroupRepository.GetAllAsync();
        var groupsWithCounts = new List<object>();

        foreach (var group in groups)
        {
            var reports = await reportRepository.GetByKpiGroupAsync(group.Id);
            groupsWithCounts.Add(new
            {
                group.Id,
                group.Name,
                group.Description,
                group.CreatedAt,
                ReportCount = reports.Count()
            });
        }

        return Ok(ApiResponse<object>.Ok(groupsWithCounts));
    }

    /// <summary>
    /// Returns the full KPI registry with all identified KPIs and their metrics.
    /// </summary>
    [HttpGet("api/analysis/getkpiregistry")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetKpiRegistry()
    {
        var registry = await _kpiRegistryRepository.GetAllAsync();
        return Ok(ApiResponse<object>.Ok(registry));
    }

    /// <summary>
    /// Triggers duplicate report detection using Azure OpenAI Embeddings (text-embedding-3-large).
    /// </summary>
    [HttpPost("api/analysis/findduplicates")]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status202Accepted)]
    public IActionResult TriggerDuplicateDetection([FromServices] DuplicateDetectionService duplicateService)
    {
        var jobId = _backgroundJobs.Enqueue(() => duplicateService.FindDuplicatesAsync(CancellationToken.None));
        _logger.LogInformation("Duplicate detection job enqueued: {JobId}", jobId);
        return Accepted(ApiResponse<string>.Ok(jobId, "Duplicate detection job started"));
    }

    /// <summary>
    /// Returns all detected duplicate report pairs, optionally filtered by minimum similarity score.
    /// </summary>
    /// <param name="minSimilarity">Minimum cosine similarity threshold (0.0–1.0). Defaults to configured value.</param>
    [HttpGet("api/analysis/getduplicates")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDuplicates([FromQuery] double? minSimilarity = null)
    {
        var duplicates = await _duplicateRepository.GetAllAsync(minSimilarity);
        return Ok(ApiResponse<object>.Ok(new
        {
            Duplicates = duplicates,
            Total = duplicates.Count()
        }));
    }
}
