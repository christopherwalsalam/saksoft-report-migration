namespace ReportConversion.Application.Settings;

public class AzureBlobSettings
{
    public string ConnectionString { get; set; } = string.Empty;
    public string ContainerName { get; set; } = "powerbi-reports";
}
