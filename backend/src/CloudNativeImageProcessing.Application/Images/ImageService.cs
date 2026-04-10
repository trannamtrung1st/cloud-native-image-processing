using CloudNativeImageProcessing.Application.Abstractions;
using CloudNativeImageProcessing.Domain.Entities;
using CloudNativeImageProcessing.Domain.Enums;

namespace CloudNativeImageProcessing.Application.Images;

public sealed class ImageService
{
    private readonly IImageRepository _repository;
    private readonly IBlobStorageService _blobStorage;
    private readonly IImageEventPublisher _eventPublisher;

    public ImageService(
        IImageRepository repository,
        IBlobStorageService blobStorage,
        IImageEventPublisher eventPublisher)
    {
        _repository = repository;
        _blobStorage = blobStorage;
        _eventPublisher = eventPublisher;
    }

    public async Task<PagedResult<ImageDto>> ListAsync(
        string userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

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
        var image = await _repository.GetByIdAsync(id, userId, cancellationToken);
        return image is null ? null : Map(image);
    }

    public async Task<ImageDto> CreateAsync(ImageUploadCommand command, string userId, CancellationToken cancellationToken)
    {
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
            await _eventPublisher.PublishImageProcessingRequestedAsync(image, cancellationToken);
        }

        if (command.UseAiDescription)
        {
            await _eventPublisher.PublishAiDescriptionRequestedAsync(image, manualDescription, cancellationToken);
        }

        return Map(image);
    }

    public async Task<bool> DeleteAsync(Guid id, string userId, CancellationToken cancellationToken)
    {
        var image = await _repository.GetByIdAsync(id, userId, cancellationToken);
        if (image is null)
        {
            return false;
        }

        await _blobStorage.DeleteAsync(image.BlobPath, cancellationToken);
        await _repository.DeleteAsync(image, cancellationToken);
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
            return null;
        }

        var stream = await _blobStorage.OpenReadAsync(image.BlobPath, cancellationToken);
        if (stream is null)
        {
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
