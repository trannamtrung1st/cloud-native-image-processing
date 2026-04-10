namespace CloudNativeImageProcessing.Application.Options;

/// <summary>Optional artificial latency for demos (e.g. before Redis caching).</summary>
public sealed class DemoOptions
{
    public const string SectionName = "Demo";

    /// <summary>Delay before loading by id for GET /api/images/{id} (milliseconds). Capped at 60000 in code.</summary>
    public int GetByIdDelayMs { get; set; }

    /// <summary>Delay inside grayscale processing in the worker (milliseconds). Max 60000.</summary>
    public int GrayscaleProcessingDelayMs { get; set; }

    /// <summary>Delay in the AI description worker before applying logic (milliseconds). Max 60000.</summary>
    public int AiDescriptionProcessingDelayMs { get; set; }
}
