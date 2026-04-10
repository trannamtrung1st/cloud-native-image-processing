using CloudNativeImageProcessing.Application.Abstractions;
using CloudNativeImageProcessing.Infrastructure.Options;
using CloudNativeImageProcessing.Infrastructure.Persistence;
using CloudNativeImageProcessing.Infrastructure.Repositories;
using CloudNativeImageProcessing.Infrastructure.Services;
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

    private static bool HasEventHub(IConfiguration configuration)
    {
        var eh = configuration.GetSection(EventHubOptions.SectionName);
        return !string.IsNullOrWhiteSpace(eh["ImageProcessingConnectionString"]) ||
               !string.IsNullOrWhiteSpace(eh["AiDescriptionConnectionString"]);
    }
}
