using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReportConversion.Application.Interfaces;
using ReportConversion.Application.Settings;
using ReportConversion.Domain.Entities;
using ReportConversion.Domain.Enums;
using System.Net.Http.Headers;
using System.Text.Json;

namespace ReportConversion.Infrastructure.SapBoClient;

public class SapBoHttpClient : ISapBoClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SapBoHttpClient> _logger;
    private readonly SapBoSettings _settings;

    public SapBoHttpClient(
        HttpClient httpClient,
        ILogger<SapBoHttpClient> logger,
        IOptions<SapBoSettings> settings)
    {
        _httpClient = httpClient;
        _logger = logger;
        _settings = settings.Value;
    }

    public async Task<string> AuthenticateAsync()
    {
        var authPayload = new
        {
            userName = _settings.Username,
            password = _settings.Password,
            auth = _settings.AuthType
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"{_settings.BaseUrl}/biprws/logon/long",
            authPayload);

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(content);
        var token = doc.RootElement.GetProperty("logonToken").GetString()
            ?? throw new InvalidOperationException("Failed to obtain SAP BO logon token");

        _logger.LogInformation("Authenticated with SAP BusinessObjects");
        return token;
    }

    public async Task<IEnumerable<Report>> GetReportsPageAsync(int page, int pageSize, string token)
    {
        _httpClient.DefaultRequestHeaders.Remove("X-SAP-LogonToken");
        _httpClient.DefaultRequestHeaders.Add("X-SAP-LogonToken", $"\"{token}\"");

        var offset = (page - 1) * pageSize;
        var url = $"{_settings.BaseUrl}/biprws/raylight/v1/documents?offset={offset}&limit={pageSize}";
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(content);

        var reports = new List<Report>();
        if (doc.RootElement.TryGetProperty("entries", out var entries))
        {
            foreach (var entry in entries.EnumerateArray())
            {
                reports.Add(MapToReport(entry));
            }
        }

        return reports;
    }

    public async Task<Report> GetReportDetailsAsync(string sapReportId, string token)
    {
        _httpClient.DefaultRequestHeaders.Remove("X-SAP-LogonToken");
        _httpClient.DefaultRequestHeaders.Add("X-SAP-LogonToken", $"\"{token}\"");

        var url = $"{_settings.BaseUrl}/biprws/raylight/v1/documents/{sapReportId}";
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(content);

        return MapToReport(doc.RootElement);
    }

    public async Task<IEnumerable<ReportElement>> GetReportElementsAsync(string sapReportId, string token)
    {
        _httpClient.DefaultRequestHeaders.Remove("X-SAP-LogonToken");
        _httpClient.DefaultRequestHeaders.Add("X-SAP-LogonToken", $"\"{token}\"");

        var elements = new List<ReportElement>();

        try
        {
            var url = $"{_settings.BaseUrl}/biprws/raylight/v1/documents/{sapReportId}/pages/1/elements";
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return elements;

            var content = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(content);

            if (doc.RootElement.TryGetProperty("elements", out var elementsArray))
            {
                foreach (var el in elementsArray.EnumerateArray())
                {
                    elements.Add(new ReportElement
                    {
                        ElementType = el.TryGetProperty("type", out var t) ? t.GetString() ?? "Unknown" : "Unknown",
                        ElementName = el.TryGetProperty("name", out var n) ? n.GetString() : null,
                        Properties = el.GetRawText(),
                        PowerBiEquivalent = MapElementType(el.TryGetProperty("type", out var et) ? et.GetString() : null)
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not retrieve elements for report {SapReportId}", sapReportId);
        }

        return elements;
    }

    public async Task<IEnumerable<ReportSql>> GetReportSqlAsync(string sapReportId, string token)
    {
        _httpClient.DefaultRequestHeaders.Remove("X-SAP-LogonToken");
        _httpClient.DefaultRequestHeaders.Add("X-SAP-LogonToken", $"\"{token}\"");

        var sqls = new List<ReportSql>();
        try
        {
            var url = $"{_settings.BaseUrl}/biprws/raylight/v1/documents/{sapReportId}/dataproviders";
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return sqls;

            var content = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(content);

            if (doc.RootElement.TryGetProperty("dataproviders", out var dps))
            {
                foreach (var dp in dps.EnumerateArray())
                {
                    var sqlText = dp.TryGetProperty("query", out var q) ? q.GetString() : null;
                    sqls.Add(new ReportSql
                    {
                        SqlText = sqlText,
                        SqlFingerprint = sqlText != null ? ComputeFingerprint(sqlText) : null,
                        QueryName = dp.TryGetProperty("name", out var n) ? n.GetString() : null
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not retrieve SQL for report {SapReportId}", sapReportId);
        }

        return sqls;
    }

    public async Task<IEnumerable<ReportDataSource>> GetReportDataSourcesAsync(string sapReportId, string token)
    {
        _httpClient.DefaultRequestHeaders.Remove("X-SAP-LogonToken");
        _httpClient.DefaultRequestHeaders.Add("X-SAP-LogonToken", $"\"{token}\"");

        var dataSources = new List<ReportDataSource>();
        try
        {
            var url = $"{_settings.BaseUrl}/biprws/raylight/v1/documents/{sapReportId}/dataproviders";
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return dataSources;

            var content = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(content);

            if (doc.RootElement.TryGetProperty("dataproviders", out var dps))
            {
                foreach (var dp in dps.EnumerateArray())
                {
                    dataSources.Add(new ReportDataSource
                    {
                        DataSourceName = dp.TryGetProperty("name", out var n) ? n.GetString() ?? "Unknown" : "Unknown",
                        DataSourceType = dp.TryGetProperty("type", out var t) ? t.GetString() : null,
                        ServerName = dp.TryGetProperty("server", out var s) ? s.GetString() : null,
                        DatabaseName = dp.TryGetProperty("database", out var db) ? db.GetString() : null
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not retrieve data sources for report {SapReportId}", sapReportId);
        }

        return dataSources;
    }

    private static Report MapToReport(JsonElement el)
    {
        var report = new Report
        {
            SapReportId = el.TryGetProperty("id", out var id) ? id.GetString() ?? string.Empty : string.Empty,
            Name = el.TryGetProperty("name", out var name) ? name.GetString() ?? string.Empty : string.Empty,
            Description = el.TryGetProperty("description", out var desc) ? desc.GetString() : null,
            Owner = el.TryGetProperty("owner", out var owner) ? owner.GetString() : null,
            FolderPath = el.TryGetProperty("cuid", out var cuid) ? cuid.GetString() : null,
            CreatedDate = el.TryGetProperty("creationDate", out var cd) && cd.TryGetDateTime(out var cdt) ? cdt : DateTime.UtcNow,
            LastModifiedDate = el.TryGetProperty("lastModificationDate", out var lmd) && lmd.TryGetDateTime(out var lmdt) ? lmdt : DateTime.UtcNow,
            LastRunDate = el.TryGetProperty("lastSuccessfulInstanceDate", out var lrd) && lrd.TryGetDateTime(out var lrdt) ? lrdt : null,
        };

        if (el.TryGetProperty("type", out var typeEl))
        {
            var typeStr = typeEl.GetString()?.ToLower();
            report.ReportType = typeStr switch
            {
                "crystalreport" or "crystal" => ReportType.CrystalReport,
                "webi" or "webintelligence" => ReportType.WebIntelligence,
                _ => ReportType.Unknown
            };
        }

        return report;
    }

    private static string MapElementType(string? sapType) => sapType?.ToLower() switch
    {
        "table" or "crosstab" or "vtable" => "Table Visual",
        "chart" or "bar" or "line" or "pie" => "Bar/Line/Pie Chart",
        "matrix" or "cross-tab" => "Matrix Visual",
        "graph" or "scatter" => "Scatter/Line Chart",
        "image" => "Image",
        _ => "Custom Visual"
    };

    private static string ComputeFingerprint(string sql)
    {
        using var md5 = System.Security.Cryptography.MD5.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(sql.Trim().ToUpper());
        return Convert.ToHexString(md5.ComputeHash(bytes))[..16];
    }
}
