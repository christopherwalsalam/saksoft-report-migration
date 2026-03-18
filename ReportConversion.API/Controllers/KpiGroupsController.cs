using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReportConversion.Application.Interfaces;
using ReportConversion.Application.Models;
using ReportConversion.Application.Services;

namespace ReportConversion.API.Controllers;

/// <summary>
/// UI-facing KPI group endpoints — triggers grouping and returns groups with their reports.
/// </summary>
[ApiController]
[Route("api/kpi-groups")]
[Authorize]
[Produces("application/json")]
public class KpiGroupsController : ControllerBase
{
    private readonly IBackgroundTaskRepository _taskRepository;
    private readonly IKpiGroupRepository _kpiGroupRepository;
    private readonly IReportRepository _reportRepository;
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly ILogger<KpiGroupsController> _logger;

    public KpiGroupsController(
        IBackgroundTaskRepository taskRepository,
        IKpiGroupRepository kpiGroupRepository,
        IReportRepository reportRepository,
        IBackgroundJobClient backgroundJobs,
        ILogger<KpiGroupsController> logger)
    {
        _taskRepository = taskRepository;
        _kpiGroupRepository = kpiGroupRepository;
        _reportRepository = reportRepository;
        _backgroundJobs = backgroundJobs;
        _logger = logger;
    }

    /// <summary>
    /// Triggers KPI grouping via Azure OpenAI GPT-4o. Returns a taskId for polling.
    /// </summary>
    [HttpPost("start")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> StartKpiGrouping([FromServices] KpiGroupingService kpiGroupingService)
    {
        var taskId = await _taskRepository.CreateAsync("KpiGrouping");
        _backgroundJobs.Enqueue(() => kpiGroupingService.GroupReportsByKpiWithTrackingAsync(taskId, CancellationToken.None));
        _logger.LogInformation("KPI grouping task started: {TaskId}", taskId);
        return Accepted(ApiResponse<object>.Ok(new { taskId }, "KPI grouping started"));
    }

    /// <summary>
    /// Returns paginated KPI groups with report counts and optional search filter.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetKpiGroups(
        [FromQuery] string? search = null,
        [FromQuery] string? kpiLabel = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);
        page = Math.Max(1, page);

        var allGroups = await _kpiGroupRepository.GetAllAsync();

        // Filter by search/kpiLabel
        var filtered = allGroups.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(search))
            filtered = filtered.Where(g => g.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(kpiLabel))
            filtered = filtered.Where(g => g.Name.Contains(kpiLabel, StringComparison.OrdinalIgnoreCase)
                                        || (g.Description != null && g.Description.Contains(kpiLabel, StringComparison.OrdinalIgnoreCase)));

        var filteredList = filtered.ToList();
        var total = filteredList.Count;
        var paged = filteredList.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        var groupsWithCounts = new List<object>();
        foreach (var g in paged)
        {
            var reports = await _reportRepository.GetByKpiGroupAsync(g.Id);
            groupsWithCounts.Add(new
            {
                g.Id,
                g.Name,
                KpiLabel = g.Description ?? g.Name,
                ReportCount = reports.Count(),
                CreatedAt = g.CreatedAt
            });
        }

        return Ok(new PagedResponse<object>
        {
            Success = true,
            Message = "KPI groups retrieved",
            Data = groupsWithCounts,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        });
    }

    /// <summary>
    /// Returns all reports within a specific KPI group, with full metadata and SQL.
    /// </summary>
    [HttpGet("{groupId:int}/reports")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetKpiGroupReports(
        int groupId,
        [FromServices] IReportSqlRepository sqlRepository)
    {
        var group = await _kpiGroupRepository.GetByIdAsync(groupId);
        if (group == null)
            return NotFound(ApiResponse<object>.Fail($"KPI group {groupId} not found"));

        var reports = await _reportRepository.GetByKpiGroupAsync(groupId);
        var enriched = new List<object>();

        foreach (var r in reports)
        {
            var sqls = await sqlRepository.GetByReportIdAsync(r.Id);
            enriched.Add(new
            {
                r.Id,
                r.Name,
                ReportType = r.ReportType,
                r.FolderPath,
                r.CreatedDate,
                r.LastRunDate,
                r.UsageStatus,
                r.IsScheduled,
                r.HasActiveSubscriptions,
                r.KpiGroupName,
                r.Description,
                r.Owner,
                SqlQueries = sqls.Select(s => s.SqlText).Where(q => !string.IsNullOrWhiteSpace(q)).ToList()
            });
        }

        return Ok(ApiResponse<object>.Ok(enriched));
    }
}
