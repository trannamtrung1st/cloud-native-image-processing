namespace CloudNativeImageProcessing.Application.Abstractions;

/// <summary>Azure Computer Vision v3.2 Analyze Image — description/caption from image bytes.</summary>
public interface IComputerVisionDescriptionClient
{
    /// <summary>Returns the best caption text, or null if the service returned none or the call failed.</summary>
    Task<string?> GetDescriptionFromImageAsync(Stream imageContent, CancellationToken cancellationToken);
}
