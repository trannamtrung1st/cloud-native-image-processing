using System.Text.Json;
using CloudNativeImageProcessing.Application.Abstractions;
using CloudNativeImageProcessing.Application.Images;
using CloudNativeImageProcessing.Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CloudNativeImageProcessing.AiGenerationWorker;

public sealed class AiGenerationEventHandler
{
    /// <summary>Stored as image description when Azure Computer Vision is not configured.</summary>
    public const string NotConnectedDescription =
        "Computer Vision not configured. Set Endpoint and ApiKey for AI descriptions.";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly IImageRepository _repository;
    private readonly IOptions<DemoOptions> _demoOptions;
    private readonly IOptions<ComputerVisionOptions> _computerVisionOptions;
    private readonly ILogger<AiGenerationEventHandler> _logger;

    public AiGenerationEventHandler(
        IImageRepository repository,
        IOptions<DemoOptions> demoOptions,
        IOptions<ComputerVisionOptions> computerVisionOptions,
        ILogger<AiGenerationEventHandler> logger)
    {
        _repository = repository;
        _demoOptions = demoOptions;
        _computerVisionOptions = computerVisionOptions;
        _logger = logger;
    }

    public async Task HandleAsync(string json, CancellationToken cancellationToken)
    {
        AiDescriptionRequestedEvent? evt;
        try
        {
            evt = JsonSerializer.Deserialize<AiDescriptionRequestedEvent>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid AI description event JSON.");
            return;
        }

        if (evt is null)
        {
            return;
        }

        var delayMs = ClampDelayMs(_demoOptions.Value.AiDescriptionProcessingDelayMs);
        if (delayMs > 0)
        {
            await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
        }

        var image = await _repository.GetByIdAsync(evt.ImageId, evt.UserId, cancellationToken).ConfigureAwait(false);
        if (image is null)
        {
            _logger.LogWarning("AI description event for missing image {ImageId}.", evt.ImageId);
            return;
        }

        if (ComputerVisionOptions.IsConfigured(_computerVisionOptions.Value))
        {
            _logger.LogInformation(
                "Computer Vision is configured; SDK integration not implemented yet for image {ImageId}.",
                evt.ImageId);
            return;
        }

        image.Description = NotConnectedDescription;
        await _repository.UpdateAsync(image, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(
            "Set placeholder AI description for image {ImageId} (Computer Vision not configured).",
            evt.ImageId);
    }

    private static int ClampDelayMs(int ms) => Math.Clamp(ms, 0, 60_000);
}
