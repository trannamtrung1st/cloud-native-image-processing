using CloudNativeImageProcessing.Domain.Enums;

namespace CloudNativeImageProcessing.Domain.Entities;

public sealed class ImageRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = "demo-user";
    public string Name { get; set; } = string.Empty;
    public string BlobPath { get; set; } = string.Empty;
    public string? PreviewUrl { get; set; }
    public string? Description { get; set; }
    public bool UseAiDescription { get; set; } = true;
    public ImageOperationType Operation { get; set; } = ImageOperationType.None;
    public string Status { get; set; } = "Queued";
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
