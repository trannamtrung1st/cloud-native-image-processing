using CloudNativeImageProcessing.Application.Abstractions;
using CloudNativeImageProcessing.Application.Options;
using CloudNativeImageProcessing.Infrastructure.Options;
using CloudNativeImageProcessing.Infrastructure.Persistence;
using CloudNativeImageProcessing.Infrastructure.Repositories;
using CloudNativeImageProcessing.Infrastructure.Services;
using StackExchange.Redis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CloudNativeImageProcessing.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres");
        services.AddDbContext<ImageDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.Configure<BlobStorageOptions>(configuration.GetSection(BlobStorageOptions.SectionName));
        services.Configure<EventHubOptions>(configuration.GetSection(EventHubOptions.SectionName));
        services.Configure<DemoOptions>(configuration.GetSection(DemoOptions.SectionName));
        services.Configure<ComputerVisionOptions>(configuration.GetSection(ComputerVisionOptions.SectionName));
        services.Configure<RedisOptions>(configuration.GetSection(RedisOptions.SectionName));

        var redisConn = configuration[$"{RedisOptions.SectionName}:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(redisConn))
        {
            services.AddSingleton<IConnectionMultiplexer>(_ =>
                ConnectionMultiplexer.Connect(redisConn));
            services.AddSingleton<IImageDetailsCache, RedisImageDetailsCache>();
        }
        else
        {
            services.AddSingleton<IImageDetailsCache, NoOpImageDetailsCache>();
        }

        var blobConn = configuration[$"{BlobStorageOptions.SectionName}:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(blobConn))
        {
            services.AddSingleton<IBlobStorageService, AzureBlobStorageService>();
        }
        else
        {
            services.AddSingleton<IBlobStorageService, LocalBlobStorageService>();
        }

        if (HasEventHub(configuration))
        {
            services.AddSingleton<EventHubImageEventPublisher>();
            services.AddSingleton<IImageEventPublisher>(sp => sp.GetRequiredService<EventHubImageEventPublisher>());
        }
        else
        {
            services.AddSingleton<IImageEventPublisher, NoOpImageEventPublisher>();
        }

        services.AddScoped<IImageRepository, PostgresImageRepository>();
        return services;
    }

    /// <summary>Registers Postgres, blob storage, and repository for the image-processing worker (no API/Event Hub publisher).</summary>
    public static IServiceCollection AddWorkerInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres");
        services.AddDbContext<ImageDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.Configure<BlobStorageOptions>(configuration.GetSection(BlobStorageOptions.SectionName));
        services.Configure<EventHubOptions>(configuration.GetSection(EventHubOptions.SectionName));
        services.Configure<DemoOptions>(configuration.GetSection(DemoOptions.SectionName));
        services.Configure<ComputerVisionOptions>(configuration.GetSection(ComputerVisionOptions.SectionName));

        var blobConn = configuration[$"{BlobStorageOptions.SectionName}:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(blobConn))
        {
            services.AddSingleton<IBlobStorageService, AzureBlobStorageService>();
        }
        else
        {
            services.AddSingleton<IBlobStorageService, LocalBlobStorageService>();
        }

        services.AddScoped<IImageRepository, PostgresImageRepository>();
        return services;
    }

    private static bool HasEventHub(IConfiguration configuration)
    {
        var eh = configuration.GetSection(EventHubOptions.SectionName);
        return !string.IsNullOrWhiteSpace(eh["ImageProcessingConnectionString"]) ||
               !string.IsNullOrWhiteSpace(eh["AiDescriptionConnectionString"]);
    }
}
