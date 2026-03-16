using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReportConversion.Application.Interfaces;
using ReportConversion.Application.Models;
using ReportConversion.Application.Settings;
using System.Text;
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

    /// <summary>
    /// Sends SQL summaries for a batch of reports to GPT-4o and asks it to discover
    /// emergent KPI groups purely from the SQL technical patterns.
    /// No predefined category list is used — the LLM invents group names based on
    /// what the SQL actually measures (tables, columns, aggregations, joins).
    /// </summary>
    public async Task<List<SqlKpiGroup>> DiscoverKpiGroupsFromSqlAsync(
        List<(int ReportId, string SqlSummary)> reports)
    {
        if (!reports.Any()) return new List<SqlKpiGroup>();

        var prompt = BuildSqlGroupingPrompt(reports);

        var options = new ChatCompletionsOptions
        {
            DeploymentName = _settings.ChatDeploymentName,
            Messages =
            {
                new ChatRequestSystemMessage("""
                    You are a senior data architect analysing SQL queries from a legacy SAP BusinessObjects environment.

                    Your task is to identify which reports measure the same or closely related KPIs by examining:
                    - The database tables they reference
                    - The columns they SELECT, GROUP BY, or filter on
                    - The aggregation functions used (SUM, COUNT, AVG, etc.)
                    - The join patterns between tables
                    - The filter conditions applied

                    Group reports that share the same core business measurement (e.g. all reports that
                    aggregate revenue by date from similar tables, or all reports measuring headcount
                    from HR tables). The group names must be derived from the SQL content — do NOT use
                    generic labels like "Financial Performance". Instead use specific names like
                    "Monthly Revenue by Customer Segment" or "Headcount Variance by Department".

                    Respond ONLY with valid JSON in this exact shape:
                    {
                      "groups": [
                        {
                          "name": "<specific KPI name derived from SQL patterns>",
                          "description": "<technical description: which tables/columns define this KPI>",
                          "reason": "<explicit explanation of WHY these reports are grouped together — cite the specific shared tables, columns, aggregation functions, join patterns, or filter fields that prove they measure the same KPI>",
                          "reportIds": [<integer>, ...]
                        }
                      ]
                    }

                    Every report ID in the input must appear in exactly one group.
                    Use "Ungrouped / No SQL Pattern Match" for reports whose SQL does not clearly
                    indicate a shared KPI with any other report.
                    """),
                new ChatRequestUserMessage(prompt)
            },
            MaxTokens = 4000,
            Temperature = 0.1f,
            ResponseFormat = ChatCompletionsResponseFormat.JsonObject
        };

        try
        {
            var response = await _client.GetChatCompletionsAsync(options);
            var content = response.Value.Choices[0].Message.Content ?? "{}";
            return ParseSqlGroupingResponse(content, reports);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DiscoverKpiGroupsFromSqlAsync failed — returning empty list");
            return new List<SqlKpiGroup>();
        }
    }

    private static string BuildSqlGroupingPrompt(List<(int ReportId, string SqlSummary)> reports)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Analyse the following report SQL summaries and group them by the KPI they measure:\n");

        foreach (var (id, summary) in reports)
        {
            sb.AppendLine($"--- Report ID: {id} ---");
            sb.AppendLine(summary);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private List<SqlKpiGroup> ParseSqlGroupingResponse(
        string json,
        List<(int ReportId, string SqlSummary)> inputReports)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("groups", out var groupsElement))
            {
                _logger.LogWarning("LLM response missing 'groups' property");
                return new List<SqlKpiGroup>();
            }

            var result = new List<SqlKpiGroup>();
            var assignedIds = new HashSet<int>();

            foreach (var groupEl in groupsElement.EnumerateArray())
            {
                var name        = groupEl.TryGetProperty("name",        out var n) ? n.GetString() ?? "Unknown" : "Unknown";
                var description = groupEl.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
                var reason      = groupEl.TryGetProperty("reason",      out var r) ? r.GetString() ?? "" : "";

                var reportIds = new List<int>();
                if (groupEl.TryGetProperty("reportIds", out var idsEl))
                {
                    foreach (var idEl in idsEl.EnumerateArray())
                    {
                        if (idEl.TryGetInt32(out int rid))
                        {
                            reportIds.Add(rid);
                            assignedIds.Add(rid);
                        }
                    }
                }

                if (reportIds.Any())
                {
                    result.Add(new SqlKpiGroup
                    {
                        Name = name,
                        Description = description,
                        Reason = reason,
                        ReportIds = reportIds
                    });
                }
            }

            // Safety net: any report not assigned by LLM goes into a catch-all group
            var unassigned = inputReports
                .Select(r => r.ReportId)
                .Where(id => !assignedIds.Contains(id))
                .ToList();

            if (unassigned.Any())
            {
                result.Add(new SqlKpiGroup
                {
                    Name = "Ungrouped / No SQL Pattern Match",
                    Description = "Reports whose SQL did not clearly match any discovered KPI group.",
                    ReportIds = unassigned
                });
            }

            return result;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse LLM grouping response");
            return new List<SqlKpiGroup>();
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
