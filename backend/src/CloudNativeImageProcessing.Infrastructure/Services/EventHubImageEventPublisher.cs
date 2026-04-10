using System.Text.Json;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using CloudNativeImageProcessing.Application.Abstractions;
using CloudNativeImageProcessing.Application.Images;
using CloudNativeImageProcessing.Domain.Entities;
using CloudNativeImageProcessing.Domain.Enums;
using CloudNativeImageProcessing.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CloudNativeImageProcessing.Infrastructure.Services;

public sealed class EventHubImageEventPublisher : IImageEventPublisher, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly EventHubProducerClient? _processingClient;
    private readonly EventHubProducerClient? _aiClient;
    private readonly ILogger<EventHubImageEventPublisher> _logger;

    public EventHubImageEventPublisher(IOptions<EventHubOptions> options, ILogger<EventHubImageEventPublisher> logger)
    {
        _logger = logger;
        var o = options.Value;
        if (!string.IsNullOrWhiteSpace(o.ImageProcessingConnectionString) &&
            !string.IsNullOrWhiteSpace(o.ImageProcessingHubName))
        {
            _processingClient = new EventHubProducerClient(
                o.ImageProcessingConnectionString,
                o.ImageProcessingHubName);
            _logger.LogInformation(
                "Event Hub image-processing producer ready (hub={HubName}).",
                o.ImageProcessingHubName);
        }
        else
        {
            _logger.LogWarning(
                "Event Hub image-processing producer is not configured (missing connection string or hub name).");
        }

        if (!string.IsNullOrWhiteSpace(o.AiDescriptionConnectionString) &&
            !string.IsNullOrWhiteSpace(o.AiDescriptionHubName))
        {
            _aiClient = new EventHubProducerClient(
                o.AiDescriptionConnectionString,
                o.AiDescriptionHubName);
        }
    }

    public async Task PublishImageProcessingRequestedAsync(ImageRecord image, CancellationToken cancellationToken)
    {
        if (_processingClient is null)
        {
            _logger.LogWarning(
                "Skipping image-processing publish for {ImageId}: Event Hub producer not configured.",
                image.Id);
            return;
        }

        if (image.Operation == ImageOperationType.None)
        {
            return;
        }

        var payload = new ImageProcessingRequestedEvent(
            image.Id,
            image.UserId,
            image.BlobPath,
            image.Operation.ToString(),
            image.Name);

        await SendAsync(_processingClient, payload, cancellationToken);
        _logger.LogInformation(
            "Published image-processing event for image {ImageId}, operation {Operation}.",
            image.Id,
            image.Operation);
    }

    public async Task PublishAiDescriptionRequestedAsync(
        ImageRecord image,
        string? manualDescriptionHint,
        CancellationToken cancellationToken)
    {
        if (_aiClient is null)
        {
            return;
        }

        var hasManual = !string.IsNullOrWhiteSpace(manualDescriptionHint);
        var payload = new AiDescriptionRequestedEvent(
            image.Id,
            image.UserId,
            image.BlobPath,
            image.Name,
            hasManual,
            hasManual ? manualDescriptionHint : null);

        await SendAsync(_aiClient, payload, cancellationToken);
    }

    private static async Task SendAsync<T>(EventHubProducerClient client, T payload, CancellationToken cancellationToken)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);
        using var batch = await client.CreateBatchAsync(cancellationToken);
        if (!batch.TryAdd(new EventData(bytes)))
        {
            throw new InvalidOperationException("Event is too large for a single Event Hub batch.");
        }

        await client.SendAsync(batch, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_processingClient is not null)
        {
            await _processingClient.DisposeAsync();
        }

        if (_aiClient is not null)
        {
            await _aiClient.DisposeAsync();
        }
    }
}
