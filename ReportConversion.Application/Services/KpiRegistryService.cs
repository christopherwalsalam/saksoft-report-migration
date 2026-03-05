using Microsoft.Extensions.Logging;
using ReportConversion.Application.Interfaces;
using ReportConversion.Domain.Entities;

namespace ReportConversion.Application.Services;

public class KpiRegistryService
{
    private readonly IReportRepository _reportRepository;
    private readonly IReportSqlRepository _sqlRepository;
    private readonly IKpiGroupRepository _kpiGroupRepository;
    private readonly IKpiRegistryRepository _kpiRegistryRepository;
    private readonly IAzureOpenAiClient _openAiClient;
    private readonly ILogger<KpiRegistryService> _logger;

    public KpiRegistryService(
        IReportRepository reportRepository,
        IReportSqlRepository sqlRepository,
        IKpiGroupRepository kpiGroupRepository,
        IKpiRegistryRepository kpiRegistryRepository,
        IAzureOpenAiClient openAiClient,
        ILogger<KpiRegistryService> logger)
    {
        _reportRepository = reportRepository;
        _sqlRepository = sqlRepository;
        _kpiGroupRepository = kpiGroupRepository;
        _kpiRegistryRepository = kpiRegistryRepository;
        _openAiClient = openAiClient;
        _logger = logger;
    }

    public async Task<int> BuildKpiRegistryAsync()
    {
        var groups = await _kpiGroupRepository.GetAllAsync();
        int added = 0;

        foreach (var group in groups)
        {
            var reports = (await _reportRepository.GetByKpiGroupAsync(group.Id)).ToList();
            if (!reports.Any()) continue;

            var uniqueKpis = reports
                .SelectMany(r => ParseKpisFromName(r.Name))
                .GroupBy(k => k.ToLower())
                .Select(g => g.First())
                .ToList();

            foreach (var kpiName in uniqueKpis)
            {
                if (await _kpiRegistryRepository.ExistsAsync(kpiName, group.Id)) continue;

                var relatedReports = reports.Where(r => r.Name.Contains(kpiName, StringComparison.OrdinalIgnoreCase)).ToList();
                var dataSources = new HashSet<string>();
                foreach (var r in relatedReports)
                {
                    var sqls = await _sqlRepository.GetByReportIdAsync(r.Id);
                    foreach (var sql in sqls.Where(s => !string.IsNullOrEmpty(s.QueryName)))
                        dataSources.Add(sql.QueryName!);
                }

                await _kpiRegistryRepository.InsertAsync(new KpiRegistry
                {
                    KpiName = kpiName,
                    KpiGroupId = group.Id,
                    BusinessDomain = group.Name,
                    ReportCount = relatedReports.Count,
                    DataSourcesInvolved = string.Join(", ", dataSources),
                    CreatedAt = DateTime.UtcNow
                });
                added++;
            }
        }

        _logger.LogInformation("KPI Registry built. Added {Count} entries", added);
        return added;
    }

    private static IEnumerable<string> ParseKpisFromName(string reportName)
    {
        var words = reportName.Split(new[] { ' ', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length >= 2)
            yield return string.Join(" ", words.Take(Math.Min(3, words.Length)));
        yield return reportName;
    }
}
