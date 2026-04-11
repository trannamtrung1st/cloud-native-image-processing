using CloudNativeImageProcessing.Application.Abstractions;
using CloudNativeImageProcessing.Domain.Entities;

namespace CloudNativeImageProcessing.Infrastructure.Repositories;

public sealed class InMemoryImageRepository : IImageRepository
{
    private readonly List<ImageRecord> _images = new();

    public Task<IReadOnlyList<ImageRecord>> ListByUserAsync(
        string userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var skip = (page - 1) * pageSize;
        IReadOnlyList<ImageRecord> result = _images
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip(skip)
            .Take(pageSize)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<int> CountByUserAsync(string userId, CancellationToken cancellationToken)
    {
        var count = _images.Count(x => x.UserId == userId);
        return Task.FromResult(count);
    }

    public Task<ImageRecord?> GetByIdAsync(Guid id, string userId, CancellationToken cancellationToken)
    {
        var image = _images.FirstOrDefault(x => x.Id == id && x.UserId == userId);
        return Task.FromResult(image);
    }

    public Task AddAsync(ImageRecord image, CancellationToken cancellationToken)
    {
        _images.Add(image);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(ImageRecord image, CancellationToken cancellationToken)
    {
        var i = _images.FindIndex(x => x.Id == image.Id);
        if (i >= 0)
        {
            _images[i] = image;
        }

        return Task.CompletedTask;
    }

    public Task UpdateStatusAsync(Guid id, string userId, string status, CancellationToken cancellationToken)
    {
        var image = _images.FirstOrDefault(x => x.Id == id && x.UserId == userId);
        if (image is not null)
        {
            image.Status = status;
        }

        return Task.CompletedTask;
    }

    public Task UpdateDescriptionAsync(Guid id, string userId, string description, CancellationToken cancellationToken)
    {
        var image = _images.FirstOrDefault(x => x.Id == id && x.UserId == userId);
        if (image is not null)
        {
            image.Description = description;
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(ImageRecord image, CancellationToken cancellationToken)
    {
        _images.Remove(image);
        return Task.CompletedTask;
    }
}
