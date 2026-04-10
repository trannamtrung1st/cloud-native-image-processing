namespace CloudNativeImageProcessing.Application.Abstractions;

public interface IBlobStorageService
{
    Task<string> UploadAsync(string fileName, Stream content, CancellationToken cancellationToken);

    /// <summary>Opens an existing blob for read, or null if not found.</summary>
    Task<Stream?> OpenReadAsync(string blobPath, CancellationToken cancellationToken);

    /// <summary>Removes the blob if it exists (idempotent).</summary>
    Task DeleteAsync(string blobPath, CancellationToken cancellationToken);
}
