using CloudNativeImageProcessing.Application.Abstractions;
using CloudNativeImageProcessing.Domain.Entities;
using CloudNativeImageProcessing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CloudNativeImageProcessing.Infrastructure.Repositories;

public sealed class PostgresImageRepository : IImageRepository
{
    private readonly ImageDbContext _dbContext;

    public PostgresImageRepository(ImageDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<ImageRecord>> ListByUserAsync(
        string userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var skip = (page - 1) * pageSize;
        return await _dbContext.Images
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountByUserAsync(string userId, CancellationToken cancellationToken)
    {
        return _dbContext.Images
            .Where(x => x.UserId == userId)
            .CountAsync(cancellationToken);
    }

    public async Task<ImageRecord?> GetByIdAsync(Guid id, string userId, CancellationToken cancellationToken)
    {
        return await _dbContext.Images
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken);
    }

    public async Task AddAsync(ImageRecord image, CancellationToken cancellationToken)
    {
        await _dbContext.Images.AddAsync(image, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(ImageRecord image, CancellationToken cancellationToken)
    {
        _dbContext.Images.Remove(image);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
