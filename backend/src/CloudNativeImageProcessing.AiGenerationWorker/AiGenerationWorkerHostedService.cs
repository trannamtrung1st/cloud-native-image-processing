using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Processor;
using Azure.Storage.Blobs;
using CloudNativeImageProcessing.Infrastructure.Options;

namespace CloudNativeImageProcessing.AiGenerationWorker;

public sealed class AiGenerationWorkerHostedService : BackgroundService
{
    private static readonly SemaphoreSlim ProcessingGate = new(1, 1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AiGenerationWorkerHostedService> _logger;
    private readonly IConfiguration _configuration;
    private EventProcessorClient? _processor;

    public AiGenerationWorkerHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<AiGenerationWorkerHostedService> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var eh = _configuration.GetSection(EventHubOptions.SectionName);
        var conn = eh["AiDescriptionConnectionString"];
        var hubName = eh["AiDescriptionHubName"] ?? "ai-description";
        var group = eh["AiDescriptionConsumerGroup"] ?? "cg1";
        var checkpointContainerName = eh["CheckpointContainerName"] ?? "eh-checkpoints";
        var blobConn = _configuration[$"{BlobStorageOptions.SectionName}:ConnectionString"];

        if (string.IsNullOrWhiteSpace(conn) || string.IsNullOrWhiteSpace(blobConn))
        {
            _logger.LogWarning(
                "EventHubs:AiDescriptionConnectionString or BlobStorage:ConnectionString is not set. AI generation worker will not consume events.");
            try
            {
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException)
            {
            }

            return;
        }

        var blobService = new BlobServiceClient(blobConn);
        var checkpointContainer = blobService.GetBlobContainerClient(checkpointContainerName);
        await checkpointContainer.CreateIfNotExistsAsync(cancellationToken: stoppingToken);

        _processor = new EventProcessorClient(checkpointContainer, group, conn, hubName);
        _processor.ProcessEventAsync += OnProcessEventAsync;
        _processor.ProcessErrorAsync += OnProcessErrorAsync;

        await _processor.StartProcessingAsync(stoppingToken);
        _logger.LogInformation(
            "AI generation worker started (hub={Hub}, consumerGroup={Group}, checkpoints={Container}).",
            hubName,
            group,
            checkpointContainerName);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
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
                var handler = scope.ServiceProvider.GetRequiredService<AiGenerationEventHandler>();
                await handler.HandleAsync(json, args.CancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling AI generation event.");
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
