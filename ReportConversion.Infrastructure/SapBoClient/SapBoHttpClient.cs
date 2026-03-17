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
        var report = MapToReport(doc.RootElement);

        // --- Scheduling enrichment ---
        // The per-document endpoint may not return full scheduling detail.
        // Call the dedicated scheduling endpoint and override the flags when it
        // returns definitive data.  A 404 simply means the report is not scheduled.
        try
        {
            var schedUrl = $"{_settings.BaseUrl}/biprws/raylight/v1/documents/{sapReportId}/scheduling";
            var schedResponse = await _httpClient.GetAsync(schedUrl);

            if (schedResponse.IsSuccessStatusCode)
            {
                var schedContent = await schedResponse.Content.ReadAsStringAsync();
                var schedDoc = JsonDocument.Parse(schedContent);
                var root = schedDoc.RootElement;

                // If the scheduling endpoint returns a non-empty schedule object the
                // report has at least one active or paused schedule.
                // Supported response shapes:
                //   { "schedule": { "recurrenceType": "daily" } }
                //   { "schedules": [ ... ] }
                bool hasSchedule = false;
                if (root.TryGetProperty("schedule", out var sched))
                {
                    // recurrenceType "none" / "once" / "" means no recurring schedule
                    var rt = sched.TryGetProperty("recurrenceType", out var rt2)
                        ? rt2.GetString()?.ToLowerInvariant()
                        : null;
                    hasSchedule = rt is not (null or "" or "none" or "once");
                }
                else if (root.TryGetProperty("schedules", out var scheds))
                {
                    hasSchedule = scheds.GetArrayLength() > 0;
                }

                if (hasSchedule)
                    report.IsScheduled = true;

                // Subscription / publication check within the scheduling response
                if (root.TryGetProperty("hasSubscriptions", out var hasSub) && hasSub.GetBoolean())
                    report.HasActiveSubscriptions = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not retrieve scheduling info for report {SapReportId} — using document-level flags", sapReportId);
        }

        return report;
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

            // SAP BO field name varies by version — try all known variants in order of precedence.
            LastModifiedDate = TryParseDate(el, "lastModificationDate", "updateDate") ?? DateTime.UtcNow,

            // SAP BO last-run date also has multiple field names across versions.
            // "lastSuccessfulInstanceDate" is the 4.x standard; "lastRunDate" / "updateDate"
            // are seen in some SP and patch levels. Falls back to null (never run) only if
            // none of the fields is present and parseable.
            LastRunDate = TryParseDate(el,
                "lastSuccessfulInstanceDate",
                "lastRunDate",
                "lastSuccessDate",
                "si_success_date"),

            // "scheduleStatus" is the primary flag in SAP BO 4.x:
            //   0 or absent  → not scheduled
            //   1            → scheduled / active
            //   2            → paused / suspended
            // Some versions expose it as a bool "isScheduled".
            IsScheduled = TryGetScheduledFlag(el),

            // A report "HasActiveSubscriptions" when it has been set up to deliver output
            // to one or more recipients (publications / subscriptions).
            // SAP BO surfaces this through "hasSubscriptions", "publicationCount",
            // or a non-zero "subscriberCount".
            HasActiveSubscriptions = TryGetSubscriptionsFlag(el),
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

    /// <summary>
    /// Tries each field name in order and returns the first parseable DateTime, or null.
    /// Handles both ISO-8601 strings and SAP's legacy /Date(ms)/ format.
    /// </summary>
    private static DateTime? TryParseDate(JsonElement el, params string[] fieldNames)
    {
        foreach (var field in fieldNames)
        {
            if (!el.TryGetProperty(field, out var prop)) continue;

            // Standard ISO-8601 datetime string
            if (prop.TryGetDateTime(out var dt)) return dt;

            // SAP legacy format: "/Date(1698768000000)/"
            var raw = prop.GetString();
            if (raw != null && raw.StartsWith("/Date(", StringComparison.Ordinal))
            {
                var ms = raw[6..raw.IndexOf(')', 6)];
                if (long.TryParse(ms, out var epoch))
                    return DateTimeOffset.FromUnixTimeMilliseconds(epoch).UtcDateTime;
            }
        }
        return null;
    }

    /// <summary>
    /// Returns true when the SAP BO document response indicates the report has a
    /// recurring schedule.  Checks several field names / value shapes used across
    /// SAP BO 4.x patch levels.
    /// </summary>
    private static bool TryGetScheduledFlag(JsonElement el)
    {
        // Boolean field (some versions)
        foreach (var field in new[] { "isScheduled", "hasSchedule" })
        {
            if (el.TryGetProperty(field, out var b) && b.ValueKind == JsonValueKind.True)
                return true;
        }

        // Numeric scheduleStatus: 0 = not scheduled, 1 = active, 2 = paused
        if (el.TryGetProperty("scheduleStatus", out var status))
        {
            if (status.ValueKind == JsonValueKind.Number &&
                status.TryGetInt32(out var n) && n > 0) return true;

            if (status.ValueKind == JsonValueKind.String)
            {
                var s = status.GetString()?.ToLowerInvariant();
                if (s is "1" or "scheduled" or "active" or "paused") return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns true when the SAP BO document response indicates the report has active
    /// subscribers / publications.
    /// </summary>
    private static bool TryGetSubscriptionsFlag(JsonElement el)
    {
        foreach (var field in new[] { "hasSubscriptions", "hasActiveSubscriptions" })
        {
            if (el.TryGetProperty(field, out var b) && b.ValueKind == JsonValueKind.True)
                return true;
        }

        // Some versions expose a count instead of a boolean
        foreach (var field in new[] { "subscriberCount", "publicationCount", "subscriptionCount" })
        {
            if (el.TryGetProperty(field, out var count) &&
                count.TryGetInt32(out var n) && n > 0) return true;
        }

        return false;
    }

    private static string ComputeFingerprint(string sql)
    {
        using var md5 = System.Security.Cryptography.MD5.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(sql.Trim().ToUpper());
        return Convert.ToHexString(md5.ComputeHash(bytes))[..16];
    }
}
