using CloudNativeImageProcessing.Application.Images;

namespace CloudNativeImageProcessing.Application.Abstractions;

public interface IImageDetailsCache
{
    Task<ImageDto?> GetAsync(string userId, Guid imageId, CancellationToken cancellationToken);

    Task SetAsync(string userId, Guid imageId, ImageDto dto, CancellationToken cancellationToken);

    Task RemoveAsync(string userId, Guid imageId, CancellationToken cancellationToken);
}
