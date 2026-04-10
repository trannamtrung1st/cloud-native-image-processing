using System.Text.Json;
using CloudNativeImageProcessing.Application.Abstractions;
using CloudNativeImageProcessing.Application.Images;
using CloudNativeImageProcessing.Infrastructure.Options;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace CloudNativeImageProcessing.Infrastructure.Services;

public sealed class RedisImageDetailsCache : IImageDetailsCache
{
    private readonly IDatabase _db;
    private readonly TimeSpan _ttl;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public RedisImageDetailsCache(IConnectionMultiplexer multiplexer, IOptions<RedisOptions> options)
    {
        _db = multiplexer.GetDatabase();
        var minutes = options.Value.DetailsExpirationMinutes;
        _ttl = TimeSpan.FromMinutes(minutes > 0 ? minutes : 5);
    }

    private static RedisKey Key(string userId, Guid imageId) =>
        $"cnip:image:details:{userId}:{imageId:N}";

    public async Task<ImageDto?> GetAsync(string userId, Guid imageId, CancellationToken cancellationToken)
    {
        var value = await _db.StringGetAsync(Key(userId, imageId)).ConfigureAwait(false);
        if (value.IsNullOrEmpty)
        {
            return null;
        }

        return JsonSerializer.Deserialize<ImageDto>(value.ToString(), JsonOptions);
    }

    public async Task SetAsync(string userId, Guid imageId, ImageDto dto, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(dto, JsonOptions);
        await _db.StringSetAsync(Key(userId, imageId), json, _ttl).ConfigureAwait(false);
    }

    public async Task RemoveAsync(string userId, Guid imageId, CancellationToken cancellationToken)
    {
        await _db.KeyDeleteAsync(Key(userId, imageId)).ConfigureAwait(false);
    }
}
