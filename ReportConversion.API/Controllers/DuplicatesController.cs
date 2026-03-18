using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReportConversion.Application.Interfaces;
using ReportConversion.Application.Models;
using ReportConversion.Application.Services;

namespace ReportConversion.API.Controllers;

/// <summary>
/// UI-facing duplicate detection endpoints.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class DuplicatesController : ControllerBase
{
    private readonly IBackgroundTaskRepository _taskRepository;
    private readonly IDuplicateReportRepository _duplicateRepository;
    private readonly IReportRepository _reportRepository;
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly ILogger<DuplicatesController> _logger;

    public DuplicatesController(
        IBackgroundTaskRepository taskRepository,
        IDuplicateReportRepository duplicateRepository,
        IReportRepository reportRepository,
        IBackgroundJobClient backgroundJobs,
        ILogger<DuplicatesController> logger)
    {
        _taskRepository = taskRepository;
        _duplicateRepository = duplicateRepository;
        _reportRepository = reportRepository;
        _backgroundJobs = backgroundJobs;
        _logger = logger;
    }

    /// <summary>
    /// Triggers the three-tier duplicate detection pipeline. Returns a taskId for polling.
    /// </summary>
    [HttpPost("start")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> StartDuplicateDetection([FromServices] DuplicateDetectionService duplicateService)
    {
        var taskId = await _taskRepository.CreateAsync("DuplicateDetection");
        _backgroundJobs.Enqueue(() => duplicateService.FindDuplicatesWithTrackingAsync(taskId, CancellationToken.None));
        _logger.LogInformation("Duplicate detection task started: {TaskId}", taskId);
        return Accepted(ApiResponse<object>.Ok(new { taskId }, "Duplicate detection started"));
    }

    /// <summary>
    /// Returns paginated duplicate report records with optional search and similarity filters.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDuplicates(
        [FromQuery] string? search = null,
        [FromQuery] double? minSimilarity = null,
        [FromQuery] string? groupId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);
        page = Math.Max(1, page);

        var all = await _duplicateRepository.GetAllAsync(minSimilarity);

        // Apply additional UI filters
        var filtered = all.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(search))
            filtered = filtered.Where(d =>
                (d.Report1 != null && d.Report1.Name.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                (d.Report2 != null && d.Report2.Name.Contains(search, StringComparison.OrdinalIgnoreCase)));
        if (!string.IsNullOrWhiteSpace(groupId) && int.TryParse(groupId, out var gid))
            filtered = filtered.Where(d => d.Id == gid || d.ReportId1 == gid || d.ReportId2 == gid);

        var filteredList = filtered.ToList();
        var total = filteredList.Count;
        var paged = filteredList.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        // Project to UI-friendly shape
        var result = paged.Select(d => new
        {
            Id = d.Id,
            ReportId = d.ReportId1,
            ReportName = d.Report1?.Name ?? d.ReportId1.ToString(),
            ReportType = d.Report1?.ReportType.ToString(),
            DuplicateGroupId = d.Id,
            SimilarityScore = d.SimilarityScore,
            MatchCount = 2,  // Each DuplicateReport record represents a pair
            DetectionMethod = d.DetectionMethod.ToString()
        }).ToList();

        return Ok(new PagedResponse<object>
        {
            Success = true,
            Message = "Duplicates retrieved",
            Data = result,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        });
    }

    /// <summary>
    /// Returns all reports in a specific duplicate group, each with full metadata and SQL.
    /// </summary>
    [HttpGet("{groupId:int}/reports")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDuplicateGroupReports(
        int groupId,
        [FromServices] IReportSqlRepository sqlRepository)
    {
        var all = await _duplicateRepository.GetAllAsync(null);
        var groupPairs = all.Where(d => d.Id == groupId).ToList();

        if (!groupPairs.Any())
            return Ok(ApiResponse<object>.Ok(Array.Empty<object>()));

        // Collect unique report IDs in this group
        var reportIds = groupPairs.SelectMany(p => new[] { p.ReportId1, p.ReportId2 })
                                  .Distinct()
                                  .ToList();

        var enriched = new List<object>();
        foreach (var reportId in reportIds)
        {
            var report = await _reportRepository.GetByIdAsync(reportId);
            if (report == null) continue;

            var sqls = await sqlRepository.GetByReportIdAsync(reportId);
            var pairForReport = groupPairs.FirstOrDefault(p => p.ReportId1 == reportId || p.ReportId2 == reportId);

            enriched.Add(new
            {
                report.Id,
                report.Name,
                ReportType = report.ReportType,
                report.FolderPath,
                report.CreatedDate,
                report.LastRunDate,
                report.UsageStatus,
                report.KpiGroupName,
                report.Description,
                report.Owner,
                SimilarityScore = pairForReport?.SimilarityScore,
                DetectionMethod = pairForReport?.DetectionMethod,
                SqlQueries = sqls.Select(s => s.SqlText).Where(q => !string.IsNullOrWhiteSpace(q)).ToList()
            });
        }

        return Ok(ApiResponse<object>.Ok(enriched));
    }
}
