using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReportConversion.Application.Interfaces;
using ReportConversion.Application.Settings;
using ReportConversion.Domain.Entities;
using ReportConversion.Domain.Enums;

namespace ReportConversion.Application.Services;

public class MetadataExtractionService
{
    private readonly ISapBoClient _sapBoClient;
    private readonly IReportRepository _reportRepository;
    private readonly IReportElementRepository _elementRepository;
    private readonly IReportSqlRepository _sqlRepository;
    private readonly ILogger<MetadataExtractionService> _logger;
    private readonly SapBoSettings _settings;

    public MetadataExtractionService(
        ISapBoClient sapBoClient,
        IReportRepository reportRepository,
        IReportElementRepository elementRepository,
        IReportSqlRepository sqlRepository,
        ILogger<MetadataExtractionService> logger,
        IOptions<SapBoSettings> settings)
    {
        _sapBoClient = sapBoClient;
        _reportRepository = reportRepository;
        _elementRepository = elementRepository;
        _sqlRepository = sqlRepository;
        _logger = logger;
        _settings = settings.Value;
    }

    public async Task<ExtractionResult> ExtractAllReportsAsync(CancellationToken cancellationToken = default)
    {
        var result = new ExtractionResult();
        var token = await _sapBoClient.AuthenticateAsync();
        int page = 1;
        const int pageSize = 100;
        bool hasMore = true;

        _logger.LogInformation("Starting SAP BO metadata extraction");

        while (hasMore && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                var reports = (await _sapBoClient.GetReportsPageAsync(page, pageSize, token)).ToList();
                if (!reports.Any())
                {
                    hasMore = false;
                    break;
                }

                foreach (var report in reports)
                {
                    try
                    {
                        var details = await _sapBoClient.GetReportDetailsAsync(report.SapReportId, token);
                        details.ExtractedAt = DateTime.UtcNow;
                        details.UpdatedAt = DateTime.UtcNow;

                        var reportId = await _reportRepository.UpsertAsync(details);

                        var elements = await _sapBoClient.GetReportElementsAsync(report.SapReportId, token);
                        await _elementRepository.DeleteByReportIdAsync(reportId);
                        foreach (var element in elements)
                        {
                            element.ReportId = reportId;
                            await _elementRepository.InsertAsync(element);
                        }

                        var sqls = await _sapBoClient.GetReportSqlAsync(report.SapReportId, token);
                        await _sqlRepository.DeleteByReportIdAsync(reportId);
                        foreach (var sql in sqls)
                        {
                            sql.ReportId = reportId;
                            await _sqlRepository.InsertAsync(sql);
                        }

                        result.ExtractedCount++;
                        _logger.LogDebug("Extracted report {ReportId}: {Name}", report.SapReportId, report.Name);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to extract details for report {SapReportId}", report.SapReportId);
                        result.FailedCount++;
                    }
                }

                _logger.LogInformation("Extracted page {Page} — {Count} reports processed", page, reports.Count);
                hasMore = reports.Count == pageSize;
                page++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting page {Page}", page);
                result.Errors.Add($"Page {page}: {ex.Message}");
                break;
            }
        }

        _logger.LogInformation("Extraction complete. Extracted: {Extracted}, Failed: {Failed}", result.ExtractedCount, result.FailedCount);
        return result;
    }
}

public class ExtractionResult
{
    public int ExtractedCount { get; set; }
    public int FailedCount { get; set; }
    public List<string> Errors { get; set; } = new();
}
