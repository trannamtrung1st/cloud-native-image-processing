using CloudNativeImageProcessing.Domain.Entities;

namespace CloudNativeImageProcessing.Application.Abstractions;

public interface IImageRepository
{
    Task<IReadOnlyList<ImageRecord>> ListByUserAsync(string userId, int page, int pageSize, CancellationToken cancellationToken);
    Task<int> CountByUserAsync(string userId, CancellationToken cancellationToken);
    Task<ImageRecord?> GetByIdAsync(Guid id, string userId, CancellationToken cancellationToken);
    Task AddAsync(ImageRecord image, CancellationToken cancellationToken);
    Task UpdateAsync(ImageRecord image, CancellationToken cancellationToken);

    /// <summary>Updates only <see cref="ImageRecord.Status"/> (single-column write).</summary>
    Task UpdateStatusAsync(Guid id, string userId, string status, CancellationToken cancellationToken);

    /// <summary>Updates only <see cref="ImageRecord.Description"/> (single-column write).</summary>
    Task UpdateDescriptionAsync(Guid id, string userId, string description, CancellationToken cancellationToken);

    Task DeleteAsync(ImageRecord image, CancellationToken cancellationToken);
}
