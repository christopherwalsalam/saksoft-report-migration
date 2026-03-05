using ReportConversion.Domain.Entities;

namespace ReportConversion.Application.Interfaces;

public interface IPowerBiClient
{
    Task<string> CreateReportAsync(Report report, IEnumerable<ReportElement> elements);
    Task<string> UploadReportAsync(string filePath, string workspaceId);
    Task<bool> PublishReportAsync(string reportId, string workspaceId);
}
