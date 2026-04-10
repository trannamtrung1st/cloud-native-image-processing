using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using CloudNativeImageProcessing.Application.Abstractions;
using CloudNativeImageProcessing.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace CloudNativeImageProcessing.Infrastructure.Services;

public sealed class AzureBlobStorageService : IBlobStorageService
{
    private readonly BlobContainerClient _container;

    public AzureBlobStorageService(IOptions<BlobStorageOptions> options)
    {
        var o = options.Value;
        if (string.IsNullOrWhiteSpace(o.ConnectionString))
        {
            throw new InvalidOperationException("BlobStorage:ConnectionString is required for Azure Blob Storage.");
        }

        var service = new BlobServiceClient(o.ConnectionString);
        _container = service.GetBlobContainerClient(o.ContainerName);
    }

    public async Task<string> UploadAsync(string fileName, Stream content, CancellationToken cancellationToken)
    {
        await _container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
        var safe = Path.GetFileName(fileName);
        if (string.IsNullOrEmpty(safe))
        {
            safe = "upload.bin";
        }

        var name = $"{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid()}_{safe}";
        var client = _container.GetBlobClient(name);
        await client.UploadAsync(content, cancellationToken: cancellationToken);
        return name;
    }

    public async Task<Stream?> OpenReadAsync(string blobPath, CancellationToken cancellationToken)
    {
        var client = _container.GetBlobClient(blobPath);
        if (!await client.ExistsAsync(cancellationToken))
        {
            return null;
        }

        return await client.OpenReadAsync(cancellationToken: cancellationToken);
    }

    public async Task DeleteAsync(string blobPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(blobPath))
        {
            return;
        }

        var client = _container.GetBlobClient(blobPath);
        try
        {
            await client.DeleteIfExistsAsync(cancellationToken: cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // Blob already deleted or path mismatch; treat as success for idempotent delete.
        }
    }

    public async Task OverwriteAsync(string blobPath, Stream content, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(blobPath))
        {
            throw new ArgumentException("Blob path is required.", nameof(blobPath));
        }

        var client = _container.GetBlobClient(blobPath);
        await _container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
        await client.UploadAsync(content, overwrite: true, cancellationToken);
    }
}
