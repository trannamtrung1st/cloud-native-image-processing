namespace CloudNativeImageProcessing.Application.Images;

/// <summary>Payload for Event Hub → image processing worker.</summary>
public sealed record ImageProcessingRequestedEvent(
    Guid ImageId,
    string UserId,
    string BlobPath,
    string Operation,
    string OriginalFileName);

/// <summary>Payload for Event Hub → AI description worker.</summary>
public sealed record AiDescriptionRequestedEvent(
    Guid ImageId,
    string UserId,
    string BlobPath,
    string OriginalFileName,
    bool HasManualDescription,
    string? ManualDescription);
