using ReportConversion.Domain.Entities;

namespace ReportConversion.Application.Interfaces;

public interface ISapBoClient
{
    Task<string> AuthenticateAsync();
    Task<IEnumerable<Report>> GetReportsPageAsync(int page, int pageSize, string token);
    Task<Report> GetReportDetailsAsync(string sapReportId, string token);
    Task<IEnumerable<ReportElement>> GetReportElementsAsync(string sapReportId, string token);
    Task<IEnumerable<ReportSql>> GetReportSqlAsync(string sapReportId, string token);
    Task<IEnumerable<ReportDataSource>> GetReportDataSourcesAsync(string sapReportId, string token);
}
