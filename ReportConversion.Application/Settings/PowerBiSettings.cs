namespace ReportConversion.Application.Settings;

public class PowerBiSettings
{
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string WorkspaceId { get; set; } = string.Empty;
    public string OutputDirectory { get; set; } = "output/pbix";
}
