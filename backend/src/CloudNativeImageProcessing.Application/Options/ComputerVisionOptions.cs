namespace CloudNativeImageProcessing.Application.Options;

/// <summary>Azure AI Vision (Computer Vision) — both <see cref="Endpoint"/> and <see cref="ApiKey"/> must be set to treat the service as connected.</summary>
public sealed class ComputerVisionOptions
{
    public const string SectionName = "ComputerVision";

    public string? Endpoint { get; set; }

    public string? ApiKey { get; set; }

    public static bool IsConfigured(ComputerVisionOptions o) =>
        !string.IsNullOrWhiteSpace(o.Endpoint) && !string.IsNullOrWhiteSpace(o.ApiKey);
}
