using CloudNativeImageProcessing.Domain.Entities;

namespace CloudNativeImageProcessing.Application.Abstractions;

public interface IImageRepository
{
    Task<IReadOnlyList<ImageRecord>> ListByUserAsync(string userId, int page, int pageSize, CancellationToken cancellationToken);
    Task<int> CountByUserAsync(string userId, CancellationToken cancellationToken);
    Task<ImageRecord?> GetByIdAsync(Guid id, string userId, CancellationToken cancellationToken);
    Task AddAsync(ImageRecord image, CancellationToken cancellationToken);
    Task DeleteAsync(ImageRecord image, CancellationToken cancellationToken);
}
