using Microsoft.Extensions.Logging;
using ReportConversion.Application.Interfaces;
using ReportConversion.Domain.Entities;

namespace ReportConversion.Application.Services;

public class KpiGroupingService
{
    private readonly IAzureOpenAiClient _openAiClient;
    private readonly IReportRepository _reportRepository;
    private readonly IKpiGroupRepository _kpiGroupRepository;
    private readonly ILogger<KpiGroupingService> _logger;

    private static readonly string[] KpiCategories = {
        "Financial Performance", "Sales & Revenue", "Operations & Logistics",
        "HR & Workforce", "Customer Analytics", "Inventory & Supply Chain",
        "Compliance & Audit", "Other / Unclassified"
    };

    public KpiGroupingService(
        IAzureOpenAiClient openAiClient,
        IReportRepository reportRepository,
        IKpiGroupRepository kpiGroupRepository,
        ILogger<KpiGroupingService> logger)
    {
        _openAiClient = openAiClient;
        _reportRepository = reportRepository;
        _kpiGroupRepository = kpiGroupRepository;
        _logger = logger;
    }

    public async Task<KpiGroupingResult> GroupReportsByKpiAsync(CancellationToken cancellationToken = default)
    {
        var result = new KpiGroupingResult();
        await EnsureKpiGroupsExistAsync();

        var reports = (await _reportRepository.GetAllAsync(1, int.MaxValue)).ToList();
        const int batchSize = 20;

        for (int i = 0; i < reports.Count; i += batchSize)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var batch = reports.Skip(i).Take(batchSize).ToList();
            var metadataBatch = batch.Select(r => (r.Id, BuildMetadata(r))).ToList();

            try
            {
                var groupings = await _openAiClient.GetKpiGroupsBatchAsync(metadataBatch);

                foreach (var (reportId, groupName) in groupings)
                {
                    if (int.TryParse(reportId, out int id))
                    {
                        var group = await _kpiGroupRepository.GetByNameAsync(groupName);
                        if (group != null)
                        {
                            await _reportRepository.UpdateKpiGroupAsync(id, group.Id);
                            result.GroupedCount++;
                        }
                    }
                }

                _logger.LogInformation("Processed KPI batch {Batch}/{Total}", i / batchSize + 1, (reports.Count + batchSize - 1) / batchSize);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing KPI batch starting at index {Index}", i);
                result.FailedCount += batch.Count;
            }
        }

        return result;
    }

    private string BuildMetadata(Domain.Entities.Report report) =>
        $"Name: {report.Name}\nDescription: {report.Description}\nType: {report.ReportType}\nFolder: {report.FolderPath}";

    private async Task EnsureKpiGroupsExistAsync()
    {
        foreach (var category in KpiCategories)
        {
            var existing = await _kpiGroupRepository.GetByNameAsync(category);
            if (existing == null)
            {
                await _kpiGroupRepository.InsertAsync(new KpiGroup
                {
                    Name = category,
                    Description = $"Reports related to {category}",
                    CreatedAt = DateTime.UtcNow
                });
            }
        }
    }
}

public class KpiGroupingResult
{
    public int GroupedCount { get; set; }
    public int FailedCount { get; set; }
}
