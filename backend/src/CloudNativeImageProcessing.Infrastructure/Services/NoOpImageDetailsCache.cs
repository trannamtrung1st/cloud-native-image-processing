using CloudNativeImageProcessing.Application.Abstractions;
using CloudNativeImageProcessing.Application.Images;

namespace CloudNativeImageProcessing.Infrastructure.Services;

public sealed class NoOpImageDetailsCache : IImageDetailsCache
{
    public Task<ImageDto?> GetAsync(string userId, Guid imageId, CancellationToken cancellationToken) =>
        Task.FromResult<ImageDto?>(null);

    public Task SetAsync(string userId, Guid imageId, ImageDto dto, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task RemoveAsync(string userId, Guid imageId, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
