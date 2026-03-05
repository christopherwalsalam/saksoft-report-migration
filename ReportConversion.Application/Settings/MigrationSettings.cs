namespace ReportConversion.Application.Settings;

public class MigrationSettings
{
    public string OutputDirectory { get; set; } = "output/pbix";
    public bool UploadToBlob { get; set; } = true;
}
