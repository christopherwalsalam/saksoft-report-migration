namespace ReportConversion.Application.Settings;

public class AzureOpenAiSettings
{
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ChatDeploymentName { get; set; } = "gpt-4o";
    public string EmbeddingDeploymentName { get; set; } = "text-embedding-3-large";
    public string ApiVersion { get; set; } = "2024-02-01";
}
