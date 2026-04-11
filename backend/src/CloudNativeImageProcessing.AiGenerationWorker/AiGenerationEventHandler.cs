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

    /// <summary>Stored when Computer Vision is configured but the API did not return a usable caption.</summary>
    public const string VisionNoCaptionDescription =
        "Computer Vision did not return a description for this image.";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly IImageRepository _repository;
    private readonly IBlobStorageService _blobStorage;
    private readonly IComputerVisionDescriptionClient _computerVision;
    private readonly IOptions<DemoOptions> _demoOptions;
    private readonly IOptions<ComputerVisionOptions> _computerVisionOptions;
    private readonly ILogger<AiGenerationEventHandler> _logger;

    public AiGenerationEventHandler(
        IImageRepository repository,
        IBlobStorageService blobStorage,
        IComputerVisionDescriptionClient computerVision,
        IOptions<DemoOptions> demoOptions,
        IOptions<ComputerVisionOptions> computerVisionOptions,
        ILogger<AiGenerationEventHandler> logger)
    {
        _repository = repository;
        _blobStorage = blobStorage;
        _computerVision = computerVision;
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

        if (!ComputerVisionOptions.IsConfigured(_computerVisionOptions.Value))
        {
            await _repository
                .UpdateDescriptionAsync(image.Id, image.UserId, NotConnectedDescription, cancellationToken)
                .ConfigureAwait(false);
            _logger.LogInformation(
                "Set placeholder AI description for image {ImageId} (Computer Vision not configured).",
                evt.ImageId);
            return;
        }

        if (!image.UseAiDescription)
        {
            _logger.LogInformation("Skipping AI description for image {ImageId} (UseAiDescription is false).", evt.ImageId);
            return;
        }

        if (string.IsNullOrWhiteSpace(image.BlobPath))
        {
            _logger.LogWarning("Image {ImageId} has no blob path; cannot call Computer Vision.", evt.ImageId);
            await _repository
                .UpdateDescriptionAsync(image.Id, image.UserId, VisionNoCaptionDescription, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        await using var blobStream = await _blobStorage.OpenReadAsync(image.BlobPath, cancellationToken).ConfigureAwait(false);
        if (blobStream is null)
        {
            _logger.LogWarning("Blob not found for image {ImageId} at path {BlobPath}.", evt.ImageId, image.BlobPath);
            await _repository
                .UpdateDescriptionAsync(image.Id, image.UserId, VisionNoCaptionDescription, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var caption = await _computerVision.GetDescriptionFromImageAsync(blobStream, cancellationToken).ConfigureAwait(false);
        var description = string.IsNullOrWhiteSpace(caption) ? VisionNoCaptionDescription : caption;
        await _repository.UpdateDescriptionAsync(image.Id, image.UserId, description, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(
            "Updated AI description for image {ImageId} from Computer Vision v3.2.",
            evt.ImageId);
    }

    private static int ClampDelayMs(int ms) => Math.Clamp(ms, 0, 60_000);
}
