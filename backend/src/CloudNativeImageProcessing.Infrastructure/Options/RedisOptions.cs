namespace CloudNativeImageProcessing.Infrastructure.Options;

public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    /// <summary>StackExchange.Redis connection string (e.g. <c>localhost:6379</c>). Empty disables caching.</summary>
    public string ConnectionString { get; set; } = "";

    /// <summary>TTL for cached image details (GET by id).</summary>
    public int DetailsExpirationMinutes { get; set; } = 5;
}
