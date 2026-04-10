using CloudNativeImageProcessing.Application.Abstractions;
using CloudNativeImageProcessing.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace CloudNativeImageProcessing.Infrastructure.Services;

public sealed class LocalBlobStorageService : IBlobStorageService
{
    private readonly string _root;

    public LocalBlobStorageService(IOptions<BlobStorageOptions> options)
    {
        var path = options.Value.LocalRootPath;
        _root = Path.IsPathRooted(path)
            ? path
            : Path.Combine(AppContext.BaseDirectory, path);
    }

    public async Task<string> UploadAsync(string fileName, Stream content, CancellationToken cancellationToken)
    {
        var safe = Path.GetFileName(fileName);
        if (string.IsNullOrEmpty(safe))
        {
            safe = "upload.bin";
        }

        var relative = $"{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid()}_{safe}";
        var fullPath = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await using var fileStream = File.Create(fullPath);
        await content.CopyToAsync(fileStream, cancellationToken);
        return relative.Replace(Path.DirectorySeparatorChar, '/');
    }

    public Task<Stream?> OpenReadAsync(string blobPath, CancellationToken cancellationToken)
    {
        var normalized = blobPath.Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.Combine(_root, normalized);
        if (!File.Exists(fullPath))
        {
            return Task.FromResult<Stream?>(null);
        }

        return Task.FromResult<Stream?>(File.OpenRead(fullPath));
    }

    public Task DeleteAsync(string blobPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(blobPath))
        {
            return Task.CompletedTask;
        }

        var normalized = blobPath.Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.Combine(_root, normalized);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }
}
