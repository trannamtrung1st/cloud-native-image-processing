using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Processor;
using Azure.Storage.Blobs;
using CloudNativeImageProcessing.Infrastructure.Options;

namespace CloudNativeImageProcessing.Worker;

public sealed class ImageProcessingWorkerHostedService : BackgroundService
{
    /// <summary>
    /// EventProcessorClient invokes handlers concurrently across partitions (one per partition).
    /// This gate caps how many events are processed at once for the whole worker process.
    /// </summary>
    private static readonly SemaphoreSlim ProcessingGate = new(1, 1);

    /// <summary>
    /// Tuned for heavy per-event work: minimal local buffering.
    /// </summary>
    private static readonly EventProcessorClientOptions ProcessorOptions = new()
    {
        CacheEventCount = 1,
        PrefetchCount = 1,
    };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ImageProcessingWorkerHostedService> _logger;
    private readonly IConfiguration _configuration;
    private EventProcessorClient? _processor;

    public ImageProcessingWorkerHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<ImageProcessingWorkerHostedService> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var eh = _configuration.GetSection(EventHubOptions.SectionName);
        var conn = eh["ImageProcessingConnectionString"];
        var hubName = eh["ImageProcessingHubName"] ?? "image-processing";
        var group = eh["ImageProcessingConsumerGroup"] ?? "cg1";
        var checkpointContainerName = eh["CheckpointContainerName"] ?? "eh-checkpoints";
        var blobConn = _configuration[$"{BlobStorageOptions.SectionName}:ConnectionString"];

        if (string.IsNullOrWhiteSpace(conn) || string.IsNullOrWhiteSpace(blobConn))
        {
            _logger.LogWarning(
                "EventHubs:ImageProcessingConnectionString or BlobStorage:ConnectionString is not set. Image processing worker will not consume events.");
            try
            {
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // shutdown
            }

            return;
        }

        var blobService = new BlobServiceClient(blobConn);
        var checkpointContainer = blobService.GetBlobContainerClient(checkpointContainerName);
        await checkpointContainer.CreateIfNotExistsAsync(cancellationToken: stoppingToken);

        _processor = new EventProcessorClient(checkpointContainer, group, conn, hubName, ProcessorOptions);
        _processor.ProcessEventAsync += OnProcessEventAsync;
        _processor.ProcessErrorAsync += OnProcessErrorAsync;

        await _processor.StartProcessingAsync(stoppingToken);
        _logger.LogInformation(
            "Image processing worker started (hub={Hub}, consumerGroup={Group}, checkpoints={Container}).",
            hubName,
            group,
            checkpointContainerName);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // graceful shutdown
        }
    }

    private async Task OnProcessEventAsync(ProcessEventArgs args)
    {
        if (args.CancellationToken.IsCancellationRequested)
        {
            return;
        }

        await ProcessingGate.WaitAsync(args.CancellationToken).ConfigureAwait(false);
        try
        {
            if (args.Data is null)
            {
                await args.UpdateCheckpointAsync(args.CancellationToken).ConfigureAwait(false);
                return;
            }

            var json = args.Data.EventBody.ToString();
            if (string.IsNullOrWhiteSpace(json))
            {
                await args.UpdateCheckpointAsync(args.CancellationToken).ConfigureAwait(false);
                return;
            }

            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var handler = scope.ServiceProvider.GetRequiredService<ImageProcessingEventHandler>();
                await handler.HandleAsync(json, args.CancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error handling image-processing event (partition={PartitionId}).",
                    args.Partition.PartitionId);
            }

            await args.UpdateCheckpointAsync(args.CancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ProcessingGate.Release();
        }
    }

    private Task OnProcessErrorAsync(ProcessErrorEventArgs args)
    {
        _logger.LogError(
            args.Exception,
            "EventProcessor error (operation={Operation}, partition={PartitionId}).",
            args.Operation,
            args.PartitionId);
        return Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_processor is not null)
        {
            try
            {
                await _processor.StopProcessingAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error stopping EventProcessorClient.");
            }

            _processor.ProcessEventAsync -= OnProcessEventAsync;
            _processor.ProcessErrorAsync -= OnProcessErrorAsync;
            _processor = null;
        }

        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }
}
