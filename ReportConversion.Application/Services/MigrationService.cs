using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReportConversion.Application.Interfaces;
using ReportConversion.Application.Settings;
using ReportConversion.Domain.Entities;
using ReportConversion.Domain.Enums;

namespace ReportConversion.Application.Services;

public class MigrationService
{
    private readonly IReportRepository _reportRepository;
    private readonly IReportElementRepository _elementRepository;
    private readonly IMigrationJobRepository _jobRepository;
    private readonly IMigrationLogRepository _logRepository;
    private readonly IPowerBiClient _powerBiClient;
    private readonly IBlobStorageClient _blobStorage;
    private readonly ILogger<MigrationService> _logger;
    private readonly MigrationSettings _settings;

    public MigrationService(
        IReportRepository reportRepository,
        IReportElementRepository elementRepository,
        IMigrationJobRepository jobRepository,
        IMigrationLogRepository logRepository,
        IPowerBiClient powerBiClient,
        IBlobStorageClient blobStorage,
        ILogger<MigrationService> logger,
        IOptions<MigrationSettings> settings)
    {
        _reportRepository = reportRepository;
        _elementRepository = elementRepository;
        _jobRepository = jobRepository;
        _logRepository = logRepository;
        _powerBiClient = powerBiClient;
        _blobStorage = blobStorage;
        _logger = logger;
        _settings = settings.Value;
    }

    public async Task MigrateReportAsync(int reportId, CancellationToken cancellationToken = default)
    {
        var report = await _reportRepository.GetByIdAsync(reportId);
        if (report == null)
        {
            _logger.LogWarning("Report {ReportId} not found for migration", reportId);
            return;
        }

        var existingJob = await _jobRepository.GetByReportIdAsync(reportId);
        int jobId;
        if (existingJob == null)
        {
            jobId = await _jobRepository.InsertAsync(new MigrationJob
            {
                ReportId = reportId,
                Status = MigrationStatus.InProgress,
                StartedAt = DateTime.UtcNow
            });
        }
        else
        {
            jobId = existingJob.Id;
            await _jobRepository.UpdateStatusAsync(jobId, MigrationStatus.InProgress);
        }

        try
        {
            await LogAsync(jobId, reportId, "Info", $"Starting migration for report: {report.Name}");
            var elements = await _elementRepository.GetByReportIdAsync(reportId);

            var filePath = await _powerBiClient.CreateReportAsync(report, elements);
            await LogAsync(jobId, reportId, "Info", $"Power BI report file created: {filePath}");

            var kpiFolder = report.KpiGroupName?.Replace(" ", "-").Replace("/", "-").ToLower() ?? "unclassified";
            var blobPath = $"reports/{kpiFolder}/{reportId}.pbix";
            var uploadedPath = await _blobStorage.UploadAsync(filePath, blobPath);
            await LogAsync(jobId, reportId, "Info", $"Uploaded to blob storage: {uploadedPath}");

            await _jobRepository.UpdateStatusAsync(jobId, MigrationStatus.Success,
                outputPath: filePath, blobPath: uploadedPath);
            _logger.LogInformation("Migration succeeded for report {ReportId}", reportId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Migration failed for report {ReportId}", reportId);
            await LogAsync(jobId, reportId, "Error", $"Migration failed: {ex.Message}", ex.ToString());
            await _jobRepository.UpdateStatusAsync(jobId, MigrationStatus.Failed, errorMessage: ex.Message);
        }
    }

    public async Task MigrateAllReportsAsync(CancellationToken cancellationToken = default)
    {
        var reports = (await _reportRepository.GetAllAsync(1, int.MaxValue)).ToList();
        _logger.LogInformation("Starting migration for {Count} reports", reports.Count);

        foreach (var report in reports)
        {
            if (cancellationToken.IsCancellationRequested) break;
            await MigrateReportAsync(report.Id, cancellationToken);
        }
    }

    public async Task MigrateReportsByIdsAsync(IEnumerable<int> reportIds, CancellationToken cancellationToken = default)
    {
        foreach (var id in reportIds)
        {
            if (cancellationToken.IsCancellationRequested) break;
            await MigrateReportAsync(id, cancellationToken);
        }
    }

    private async Task LogAsync(int jobId, int reportId, string level, string message, string? details = null)
    {
        await _logRepository.InsertAsync(new MigrationLog
        {
            MigrationJobId = jobId,
            ReportId = reportId,
            LogLevel = level,
            Message = message,
            Details = details,
            Timestamp = DateTime.UtcNow
        });
    }
}
