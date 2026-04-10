namespace CloudNativeImageProcessing.Infrastructure.Options;

public sealed class EventHubOptions
{
    public const string SectionName = "EventHubs";

    public string? ImageProcessingConnectionString { get; set; }
    public string ImageProcessingHubName { get; set; } = "image-processing";

    public string? AiDescriptionConnectionString { get; set; }
    public string AiDescriptionHubName { get; set; } = "ai-description";

    /// <summary>Event Hub consumer group for the AI generation worker (<c>ai-description</c> hub). Local emulator: <c>cg1</c> (see <c>infra/eventhubs-emulator/Config.json</c>).</summary>
    public string AiDescriptionConsumerGroup { get; set; } = "cg1";

    /// <summary>Event Hub consumer group for the image-processing worker (EventProcessorClient). Local emulator: use <c>cg1</c> (see <c>infra/eventhubs-emulator/Config.json</c>). Azure: often <c>$Default</c>.</summary>
    public string ImageProcessingConsumerGroup { get; set; } = "cg1";

    /// <summary>Blob container name for Event Hub processor checkpoints (Azurite / Azure Storage).</summary>
    public string CheckpointContainerName { get; set; } = "eh-checkpoints";
}
