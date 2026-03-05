using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReportConversion.Application.Interfaces;
using ReportConversion.Application.Settings;
using ReportConversion.Domain.Enums;

namespace ReportConversion.Application.Services;

public class StaleReportService
{
    private readonly IReportRepository _reportRepository;
    private readonly ILogger<StaleReportService> _logger;
    private readonly StaleReportSettings _settings;

    public StaleReportService(
        IReportRepository reportRepository,
        ILogger<StaleReportService> logger,
        IOptions<StaleReportSettings> settings)
    {
        _reportRepository = reportRepository;
        _logger = logger;
        _settings = settings.Value;
    }

    public async Task<StaleReportResult> IdentifyStaleReportsAsync()
    {
        var result = new StaleReportResult();
        var cutoff = DateTime.UtcNow.AddDays(-_settings.StaleDaysThreshold);
        var allReports = await _reportRepository.GetAllAsync(1, int.MaxValue);

        foreach (var report in allReports)
        {
            UsageStatus status;
            if (report.LastRunDate == null && !report.IsScheduled && !report.HasActiveSubscriptions)
            {
                status = UsageStatus.NeverUsed;
                result.NeverUsedCount++;
            }
            else if ((report.LastRunDate == null || report.LastRunDate < cutoff) && !report.IsScheduled && !report.HasActiveSubscriptions)
            {
                status = UsageStatus.Stale;
                result.StaleCount++;
            }
            else
            {
                status = UsageStatus.Active;
                result.ActiveCount++;
            }

            await _reportRepository.UpdateUsageStatusAsync(report.Id, status);
        }

        _logger.LogInformation("Usage status updated. Active: {Active}, Stale: {Stale}, NeverUsed: {Never}",
            result.ActiveCount, result.StaleCount, result.NeverUsedCount);
        return result;
    }
}

public class StaleReportResult
{
    public int ActiveCount { get; set; }
    public int StaleCount { get; set; }
    public int NeverUsedCount { get; set; }
}
