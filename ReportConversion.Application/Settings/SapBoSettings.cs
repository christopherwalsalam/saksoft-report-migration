namespace ReportConversion.Application.Settings;

public class SapBoSettings
{
    public string BaseUrl { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string AuthType { get; set; } = "secEnterprise";
    public int BatchSize { get; set; } = 100;
    public int RetryCount { get; set; } = 3;
    public int RetryDelaySeconds { get; set; } = 2;
}
