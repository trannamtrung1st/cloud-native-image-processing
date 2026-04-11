using System.Text.Json;
using CloudNativeImageProcessing.Application.Abstractions;
using CloudNativeImageProcessing.Application.Images;
using CloudNativeImageProcessing.Application.Options;
using CloudNativeImageProcessing.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace CloudNativeImageProcessing.Worker;

public sealed class ImageProcessingEventHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly IImageRepository _repository;
    private readonly IBlobStorageService _blobStorage;
    private readonly ILogger<ImageProcessingEventHandler> _logger;
    private readonly IOptions<DemoOptions> _demoOptions;

    public ImageProcessingEventHandler(
        IImageRepository repository,
        IBlobStorageService blobStorage,
        ILogger<ImageProcessingEventHandler> logger,
        IOptions<DemoOptions> demoOptions)
    {
        _repository = repository;
        _blobStorage = blobStorage;
        _logger = logger;
        _demoOptions = demoOptions;
    }

    public async Task HandleAsync(string json, CancellationToken cancellationToken)
    {
        ImageProcessingRequestedEvent? evt;
        try
        {
            evt = JsonSerializer.Deserialize<ImageProcessingRequestedEvent>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Skipping invalid image-processing event payload.");
            return;
        }

        if (evt is null)
        {
            return;
        }

        var record = await _repository.GetByIdAsync(evt.ImageId, evt.UserId, cancellationToken);
        if (record is null)
        {
            _logger.LogWarning("Image {ImageId} not found for user {UserId}.", evt.ImageId, evt.UserId);
            return;
        }

        if (!string.Equals(record.Status, "Processing", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation(
                "Skipping image {ImageId}: status is {Status}, expected Processing.",
                evt.ImageId,
                record.Status);
            return;
        }

        if (!string.Equals(evt.Operation, nameof(ImageOperationType.Grayscale), StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Unsupported image operation {Operation} for {ImageId}.", evt.Operation, evt.ImageId);
            await _repository.UpdateStatusAsync(record.Id, record.UserId, "Failed", cancellationToken);
            return;
        }

        try
        {
            await using var readStream = await _blobStorage.OpenReadAsync(evt.BlobPath, cancellationToken);
            if (readStream is null)
            {
                _logger.LogWarning("Blob missing for image {ImageId} at {BlobPath}.", evt.ImageId, evt.BlobPath);
                await _repository.UpdateStatusAsync(record.Id, record.UserId, "Failed", cancellationToken);
                return;
            }

            await using var buffer = new MemoryStream();
            await readStream.CopyToAsync(buffer, cancellationToken);
            buffer.Position = 0;

            using var image = await Image.LoadAsync(buffer, cancellationToken);

            var grayscaleDelayMs = Math.Clamp(_demoOptions.Value.GrayscaleProcessingDelayMs, 0, 60_000);
            if (grayscaleDelayMs > 0)
            {
                await Task.Delay(grayscaleDelayMs, cancellationToken);
            }

            image.Mutate(x => x.Grayscale());

            await using var output = new MemoryStream();
            await SaveWithSameFormatAsync(image, evt.OriginalFileName, output, cancellationToken);
            output.Position = 0;

            await _blobStorage.OverwriteAsync(evt.BlobPath, output, cancellationToken);

            await _repository.UpdateStatusAsync(record.Id, record.UserId, "Ready", cancellationToken);

            _logger.LogInformation("Processed image {ImageId} ({Operation}).", evt.ImageId, evt.Operation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process image {ImageId}.", evt.ImageId);
            await _repository.UpdateStatusAsync(record.Id, record.UserId, "Failed", cancellationToken);
        }
    }

    private static async Task SaveWithSameFormatAsync(
        Image image,
        string originalFileName,
        Stream output,
        CancellationToken cancellationToken)
    {
        var ext = Path.GetExtension(originalFileName).ToLowerInvariant();
        IImageEncoder encoder = ext switch
        {
            ".jpg" or ".jpeg" => new JpegEncoder(),
            ".png" => new PngEncoder(),
            ".gif" => new GifEncoder(),
            ".webp" => new WebpEncoder(),
            _ => new PngEncoder(),
        };

        await image.SaveAsync(output, encoder, cancellationToken);
    }
}
