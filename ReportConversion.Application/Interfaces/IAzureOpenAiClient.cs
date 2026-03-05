namespace ReportConversion.Application.Interfaces;

public interface IAzureOpenAiClient
{
    Task<string> GetKpiGroupAsync(string reportMetadata);
    Task<List<float>> GetEmbeddingAsync(string text);
    Task<Dictionary<string, string>> GetKpiGroupsBatchAsync(List<(int Id, string Metadata)> reports);
}
