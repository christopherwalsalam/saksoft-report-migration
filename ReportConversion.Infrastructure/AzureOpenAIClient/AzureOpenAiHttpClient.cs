using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReportConversion.Application.Interfaces;
using ReportConversion.Application.Settings;
using System.Text.Json;

namespace ReportConversion.Infrastructure.AzureOpenAIClient;

public class AzureOpenAiHttpClient : IAzureOpenAiClient
{
    private readonly OpenAIClient _client;
    private readonly ILogger<AzureOpenAiHttpClient> _logger;
    private readonly AzureOpenAiSettings _settings;

    public AzureOpenAiHttpClient(
        ILogger<AzureOpenAiHttpClient> logger,
        IOptions<AzureOpenAiSettings> settings)
    {
        _logger = logger;
        _settings = settings.Value;
        _client = new OpenAIClient(
            new Uri(_settings.Endpoint),
            new AzureKeyCredential(_settings.ApiKey));
    }

    public async Task<string> GetKpiGroupAsync(string reportMetadata)
    {
        var prompt = BuildKpiPrompt(new List<(int, string)> { (0, reportMetadata) });
        var options = new ChatCompletionsOptions
        {
            DeploymentName = _settings.ChatDeploymentName,
            Messages =
            {
                new ChatRequestSystemMessage("You are a business intelligence analyst classifying SAP BO reports into KPI groups."),
                new ChatRequestUserMessage(prompt)
            },
            MaxTokens = 200,
            Temperature = 0.1f
        };

        var response = await _client.GetChatCompletionsAsync(options);
        return response.Value.Choices[0].Message.Content?.Trim() ?? "Other / Unclassified";
    }

    public async Task<List<float>> GetEmbeddingAsync(string text)
    {
        var options = new EmbeddingsOptions(_settings.EmbeddingDeploymentName, new[] { text });
        var response = await _client.GetEmbeddingsAsync(options);
        return response.Value.Data[0].Embedding.ToArray().Select(f => (float)f).ToList();
    }

    public async Task<Dictionary<string, string>> GetKpiGroupsBatchAsync(List<(int Id, string Metadata)> reports)
    {
        var prompt = BuildKpiPrompt(reports);
        var options = new ChatCompletionsOptions
        {
            DeploymentName = _settings.ChatDeploymentName,
            Messages =
            {
                new ChatRequestSystemMessage("""
                    You are a business intelligence analyst. Classify each report into exactly one KPI group.
                    Valid groups: Financial Performance, Sales & Revenue, Operations & Logistics, HR & Workforce,
                    Customer Analytics, Inventory & Supply Chain, Compliance & Audit, Other / Unclassified.
                    Return ONLY valid JSON: {"reportId": "groupName", ...}
                    """),
                new ChatRequestUserMessage(prompt)
            },
            MaxTokens = 1000,
            Temperature = 0.1f,
            ResponseFormat = ChatCompletionsResponseFormat.JsonObject
        };

        try
        {
            var response = await _client.GetChatCompletionsAsync(options);
            var content = response.Value.Choices[0].Message.Content ?? "{}";
            var result = JsonSerializer.Deserialize<Dictionary<string, string>>(content)
                ?? new Dictionary<string, string>();

            // Normalize keys to match report IDs
            return result.ToDictionary(
                kvp => kvp.Key,
                kvp => NormalizeKpiGroup(kvp.Value));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get KPI group batch from Azure OpenAI");
            return new Dictionary<string, string>();
        }
    }

    private static string BuildKpiPrompt(List<(int Id, string Metadata)> reports)
    {
        var items = reports.Select(r => $"ID:{r.Id}\n{r.Metadata}");
        return $"Classify these reports into KPI groups:\n\n{string.Join("\n---\n", items)}";
    }

    private static string NormalizeKpiGroup(string raw)
    {
        var validGroups = new[]
        {
            "Financial Performance", "Sales & Revenue", "Operations & Logistics",
            "HR & Workforce", "Customer Analytics", "Inventory & Supply Chain",
            "Compliance & Audit", "Other / Unclassified"
        };
        return validGroups.FirstOrDefault(g =>
            raw.Contains(g, StringComparison.OrdinalIgnoreCase)) ?? "Other / Unclassified";
    }
}
