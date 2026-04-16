using CloudNativeImageProcessing.Application.Abstractions;
using CloudNativeImageProcessing.Application.Options;
using CloudNativeImageProcessing.Domain.Entities;
using CloudNativeImageProcessing.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CloudNativeImageProcessing.Application.Images;

public sealed class ImageService
{
    private readonly IImageRepository _repository;
    private readonly IBlobStorageService _blobStorage;
    private readonly IImageEventPublisher _eventPublisher;
    private readonly IImageDetailsCache _detailsCache;
    private readonly IOptions<DemoOptions> _demoOptions;
    private readonly ILogger<ImageService> _logger;

    public ImageService(
        IImageRepository repository,
        IBlobStorageService blobStorage,
        IImageEventPublisher eventPublisher,
        IImageDetailsCache detailsCache,
        IOptions<DemoOptions> demoOptions,
        ILogger<ImageService> logger)
    {
        _repository = repository;
        _blobStorage = blobStorage;
        _eventPublisher = eventPublisher;
        _detailsCache = detailsCache;
        _demoOptions = demoOptions;
        _logger = logger;
    }

    public async Task<PagedResult<ImageDto>> ListAsync(
        string userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        _logger.LogDebug("Listing images for user {UserId} page={Page} pageSize={PageSize}.", userId, page, pageSize);

        var totalCount = await _repository.CountByUserAsync(userId, cancellationToken);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        var items = await _repository.ListByUserAsync(userId, page, pageSize, cancellationToken);
        return new PagedResult<ImageDto>(
            items.Select(Map).ToList(),
            page,
            pageSize,
            totalCount,
            totalPages);
    }

    public async Task<ImageDto?> GetAsync(Guid id, string userId, CancellationToken cancellationToken)
    {
        var cached = await _detailsCache.GetAsync(userId, id, cancellationToken);
        if (cached is not null)
        {
            _logger.LogDebug("Image details cache hit for {ImageId}.", id);
            return cached;
        }

        var delayMs = ClampDemoDelayMs(_demoOptions.Value.GetByIdDelayMs);
        if (delayMs > 0)
        {
            await Task.Delay(delayMs, cancellationToken);
        }

        var image = await _repository.GetByIdAsync(id, userId, cancellationToken);
        if (image is null)
        {
            _logger.LogDebug("Get image {ImageId}: not found for user {UserId}.", id, userId);
            return null;
        }

        var dto = Map(image);
        await _detailsCache.SetAsync(userId, id, dto, cancellationToken);
        return dto;
    }

    private static int ClampDemoDelayMs(int ms) => Math.Clamp(ms, 0, 60_000);

    public async Task<ImageDto> CreateAsync(
        ImageUploadCommand command,
        string userId,
        string userEmail,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userEmail))
        {
            throw new ArgumentException("User email is required.", nameof(userEmail));
        }

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            throw new ArgumentException("Name is required.", nameof(command));
        }

        var displayName = command.Name.Trim();
        var blobFileName = !string.IsNullOrWhiteSpace(command.OriginalFileName)
            ? Path.GetFileName(command.OriginalFileName)
            : Path.GetFileName(displayName);

        await using var stream = command.FileStream;
        var blobPath = await _blobStorage.UploadAsync(blobFileName, stream, cancellationToken);

        var operation = ParseOperation(command.Operation);
        var manualDescription = string.IsNullOrWhiteSpace(command.Description) ? null : command.Description.Trim();

        var image = new ImageRecord
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = displayName,
            BlobPath = blobPath,
            PreviewUrl = null,
            Description = command.UseAiDescription
                ? "AI description pending..."
                : (manualDescription ?? "No description provided."),
            UseAiDescription = command.UseAiDescription,
            Operation = operation,
            Status = operation == ImageOperationType.None ? "Ready" : "Processing",
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        await _repository.AddAsync(image, cancellationToken);

        if (operation != ImageOperationType.None)
        {
            await _eventPublisher.PublishImageProcessingRequestedAsync(image, userEmail, cancellationToken);
        }

        if (command.UseAiDescription)
        {
            await _eventPublisher.PublishAiDescriptionRequestedAsync(image, userEmail, manualDescription, cancellationToken);
        }

        _logger.LogInformation(
            "Image created {ImageId} for user {UserId}: operation={Operation}, useAiDescription={UseAi}, status={Status}.",
            image.Id,
            userId,
            operation,
            command.UseAiDescription,
            image.Status);

        return Map(image);
    }

    public async Task<bool> DeleteAsync(Guid id, string userId, CancellationToken cancellationToken)
    {
        var image = await _repository.GetByIdAsync(id, userId, cancellationToken);
        if (image is null)
        {
            _logger.LogDebug("Delete skipped: image {ImageId} not found for user {UserId}.", id, userId);
            return false;
        }

        // Remove DB row first so the API succeeds even if the blob is already gone (404) or storage is inconsistent.
        await _repository.DeleteAsync(image, cancellationToken);
        await _detailsCache.RemoveAsync(userId, id, cancellationToken);
        await _blobStorage.DeleteAsync(image.BlobPath, cancellationToken);
        _logger.LogInformation("Image {ImageId} deleted for user {UserId}.", id, userId);
        return true;
    }

    /// <summary>Opens the uploaded blob for preview (same user only).</summary>
    public async Task<(Stream Stream, string ContentType)?> GetImageBlobAsync(
        Guid id,
        string userId,
        CancellationToken cancellationToken)
    {
        var image = await _repository.GetByIdAsync(id, userId, cancellationToken);
        if (image is null)
        {
            _logger.LogDebug("Preview: image {ImageId} not found for user {UserId}.", id, userId);
            return null;
        }

        var stream = await _blobStorage.OpenReadAsync(image.BlobPath, cancellationToken);
        if (stream is null)
        {
            _logger.LogWarning(
                "Preview blob missing or unreadable for image {ImageId} at {BlobPath}.",
                id,
                image.BlobPath);
            return null;
        }

        return (stream, GuessContentType(image.Name));
    }

    private static ImageOperationType ParseOperation(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "grayscale" => ImageOperationType.Grayscale,
            _ => ImageOperationType.None
        };

    private static string GuessContentType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".svg" => "image/svg+xml",
            _ => "application/octet-stream",
        };
    }

    private static ImageDto Map(ImageRecord image) =>
        new(
            image.Id,
            image.Name,
            image.Description,
            image.Status,
            image.Operation.ToString(),
            image.CreatedAtUtc,
            string.IsNullOrWhiteSpace(image.PreviewUrl)
                ? $"/api/images/{image.Id}/preview"
                : image.PreviewUrl);
}
