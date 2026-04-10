namespace CloudNativeImageProcessing.Application.Images;

public sealed record ImageUploadCommand(
    string Name,
    string? Description,
    bool UseAiDescription,
    string Operation,
    Stream FileStream,
    string OriginalFileName,
    string? ContentType);
