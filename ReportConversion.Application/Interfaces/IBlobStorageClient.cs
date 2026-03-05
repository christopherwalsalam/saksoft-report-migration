namespace ReportConversion.Application.Interfaces;

public interface IBlobStorageClient
{
    Task<string> UploadAsync(string localFilePath, string blobPath);
    Task<bool> ExistsAsync(string blobPath);
    Task DeleteAsync(string blobPath);
}
