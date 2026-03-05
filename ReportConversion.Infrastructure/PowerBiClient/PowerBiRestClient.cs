using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using ReportConversion.Application.Interfaces;
using ReportConversion.Application.Settings;
using ReportConversion.Domain.Entities;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ReportConversion.Infrastructure.PowerBiClient;

public class PowerBiRestClient : IPowerBiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PowerBiRestClient> _logger;
    private readonly PowerBiSettings _settings;
    private readonly IConfidentialClientApplication _msalApp;

    public PowerBiRestClient(
        HttpClient httpClient,
        ILogger<PowerBiRestClient> logger,
        IOptions<PowerBiSettings> settings)
    {
        _httpClient = httpClient;
        _logger = logger;
        _settings = settings.Value;
        _msalApp = ConfidentialClientApplicationBuilder
            .Create(_settings.ClientId)
            .WithClientSecret(_settings.ClientSecret)
            .WithAuthority($"https://login.microsoftonline.com/{_settings.TenantId}")
            .Build();
    }

    private async Task<string> GetAccessTokenAsync()
    {
        var result = await _msalApp
            .AcquireTokenForClient(new[] { "https://analysis.windows.net/powerbi/api/.default" })
            .ExecuteAsync();
        return result.AccessToken;
    }

    public async Task<string> CreateReportAsync(Report report, IEnumerable<ReportElement> elements)
    {
        Directory.CreateDirectory(_settings.OutputDirectory);
        var fileName = $"{report.Id}_{SanitiseFileName(report.Name)}.pbix";
        var filePath = Path.Combine(_settings.OutputDirectory, fileName);

        // Generate Power BI template file content
        var template = GeneratePbixTemplate(report, elements);
        await File.WriteAllTextAsync(filePath, template);

        _logger.LogInformation("Created Power BI template file: {FilePath}", filePath);
        return filePath;
    }

    public async Task<string> UploadReportAsync(string filePath, string workspaceId)
    {
        var token = await GetAccessTokenAsync();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var reportName = Path.GetFileNameWithoutExtension(filePath);
        var url = $"https://api.powerbi.com/v1.0/myorg/groups/{workspaceId}/imports?datasetDisplayName={Uri.EscapeDataString(reportName)}&nameConflict=CreateOrOverwrite";

        await using var fileStream = File.OpenRead(filePath);
        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(fileStream), "file", Path.GetFileName(filePath));

        var response = await _httpClient.PostAsync(url, content);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(responseContent);
        return doc.RootElement.GetProperty("id").GetString() ?? string.Empty;
    }

    public async Task<bool> PublishReportAsync(string reportId, string workspaceId)
    {
        var token = await GetAccessTokenAsync();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Check import status
        var url = $"https://api.powerbi.com/v1.0/myorg/groups/{workspaceId}/imports/{reportId}";
        for (int i = 0; i < 10; i++)
        {
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return false;

            var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var status = doc.RootElement.GetProperty("importState").GetString();
            if (status == "Succeeded") return true;
            if (status == "Failed") return false;

            await Task.Delay(TimeSpan.FromSeconds(5));
        }

        return false;
    }

    private string GeneratePbixTemplate(Report report, IEnumerable<ReportElement> elements)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"// Power BI Template for: {report.Name}");
        sb.AppendLine($"// Migrated from SAP BusinessObjects ({report.ReportType})");
        sb.AppendLine($"// KPI Group: {report.KpiGroupName ?? "Unclassified"}");
        sb.AppendLine($"// Original ID: {report.SapReportId}");
        sb.AppendLine($"// Migration Date: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();
        sb.AppendLine("// Visual Elements:");
        foreach (var el in elements)
        {
            sb.AppendLine($"//   {el.ElementType} -> {el.PowerBiEquivalent ?? "Custom Visual"}: {el.ElementName}");
        }

        return sb.ToString();
    }

    private static string SanitiseFileName(string name) =>
        string.Join("_", name.Split(Path.GetInvalidFileNameChars())).Trim('_')[..Math.Min(name.Length, 50)];
}
