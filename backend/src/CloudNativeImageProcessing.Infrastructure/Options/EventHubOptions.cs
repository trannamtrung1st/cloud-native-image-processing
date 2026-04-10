namespace CloudNativeImageProcessing.Infrastructure.Options;

public sealed class EventHubOptions
{
    public const string SectionName = "EventHubs";

    public string? ImageProcessingConnectionString { get; set; }
    public string ImageProcessingHubName { get; set; } = "image-processing";

    public string? AiDescriptionConnectionString { get; set; }
    public string AiDescriptionHubName { get; set; } = "ai-description";
}
