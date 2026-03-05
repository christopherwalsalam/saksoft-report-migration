using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReportConversion.Application.Interfaces;
using ReportConversion.Application.Settings;

namespace ReportConversion.Infrastructure.AzureOpenAIClient;

public class AzureBlobStorageClient : IBlobStorageClient
{
    private readonly BlobContainerClient _containerClient;
    private readonly ILogger<AzureBlobStorageClient> _logger;

    public AzureBlobStorageClient(
        ILogger<AzureBlobStorageClient> logger,
        IOptions<AzureBlobSettings> settings)
    {
        _logger = logger;
        var blobSettings = settings.Value;
        var serviceClient = new BlobServiceClient(blobSettings.ConnectionString);
        _containerClient = serviceClient.GetBlobContainerClient(blobSettings.ContainerName);
    }

    public async Task<string> UploadAsync(string localFilePath, string blobPath)
    {
        await _containerClient.CreateIfNotExistsAsync(PublicAccessType.None);

        var blobClient = _containerClient.GetBlobClient(blobPath);
        await using var fileStream = File.OpenRead(localFilePath);
        await blobClient.UploadAsync(fileStream, overwrite: true);

        _logger.LogInformation("Uploaded {LocalPath} to blob {BlobPath}", localFilePath, blobPath);
        return blobClient.Uri.ToString();
    }

    public async Task<bool> ExistsAsync(string blobPath)
    {
        var blobClient = _containerClient.GetBlobClient(blobPath);
        return await blobClient.ExistsAsync();
    }

    public async Task DeleteAsync(string blobPath)
    {
        var blobClient = _containerClient.GetBlobClient(blobPath);
        await blobClient.DeleteIfExistsAsync();
        _logger.LogInformation("Deleted blob {BlobPath}", blobPath);
    }
}
