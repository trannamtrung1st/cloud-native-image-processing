using CloudNativeImageProcessing.Application.Abstractions;
using CloudNativeImageProcessing.Domain.Entities;

namespace CloudNativeImageProcessing.Infrastructure.Services;

public sealed class NoOpImageEventPublisher : IImageEventPublisher
{
    public Task PublishImageProcessingRequestedAsync(ImageRecord image, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task PublishAiDescriptionRequestedAsync(
        ImageRecord image,
        string? manualDescriptionHint,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
