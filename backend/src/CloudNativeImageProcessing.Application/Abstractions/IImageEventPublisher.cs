using CloudNativeImageProcessing.Domain.Entities;

namespace CloudNativeImageProcessing.Application.Abstractions;

public interface IImageEventPublisher
{
    /// <summary>Published when a non-None operation is selected (e.g. grayscale) for the image processing worker.</summary>
    Task PublishImageProcessingRequestedAsync(
        ImageRecord image,
        string userEmail,
        CancellationToken cancellationToken);

    /// <summary>Published when AI-generated description is requested for the AI description worker.</summary>
    Task PublishAiDescriptionRequestedAsync(
        ImageRecord image,
        string userEmail,
        string? manualDescriptionHint,
        CancellationToken cancellationToken);
}
