using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using CloudNativeImageProcessing.Application.Abstractions;
using CloudNativeImageProcessing.Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CloudNativeImageProcessing.Infrastructure.Services;

/// <summary>
/// Calls <c>POST {endpoint}/vision/v3.2/analyze?visualFeatures=Description</c> with <c>application/octet-stream</c>
/// (<see href="https://learn.microsoft.com/en-us/rest/api/computervision/analyze-image/analyze-image?view=rest-computervision-v3.2"/>).
/// </summary>
public sealed class ComputerVisionV32DescriptionClient : IComputerVisionDescriptionClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _httpClient;
    private readonly IOptions<ComputerVisionOptions> _options;
    private readonly ILogger<ComputerVisionV32DescriptionClient> _logger;

    public ComputerVisionV32DescriptionClient(
        HttpClient httpClient,
        IOptions<ComputerVisionOptions> options,
        ILogger<ComputerVisionV32DescriptionClient> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    public async Task<string?> GetDescriptionFromImageAsync(Stream imageContent, CancellationToken cancellationToken)
    {
        var o = _options.Value;
        if (!ComputerVisionOptions.IsConfigured(o))
        {
            return null;
        }

        var baseUrl = o.Endpoint!.TrimEnd('/');
        var requestUri = $"{baseUrl}/vision/v3.2/analyze?visualFeatures=Description&language=en";

        using var payload = new MemoryStream();
        await imageContent.CopyToAsync(payload, cancellationToken).ConfigureAwait(false);
        payload.Position = 0;

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        request.Headers.TryAddWithoutValidation("Ocp-Apim-Subscription-Key", o.ApiKey);

        var streamContent = new StreamContent(payload);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        request.Content = streamContent;

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Computer Vision v3.2 analyze failed ({StatusCode}): {Body}",
                (int)response.StatusCode,
                body.Length > 500 ? body[..500] + "…" : body);
            return null;
        }

        AnalyzeImageV32Response? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<AnalyzeImageV32Response>(body, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Could not parse Computer Vision analyze response.");
            return null;
        }

        var captions = parsed?.Description?.Captions;
        if (captions is null || captions.Count == 0)
        {
            return null;
        }

        var best = captions.OrderByDescending(c => c.Confidence).FirstOrDefault();
        var text = best?.Text?.Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    private sealed class AnalyzeImageV32Response
    {
        [JsonPropertyName("description")]
        public DescriptionBlock? Description { get; set; }
    }

    private sealed class DescriptionBlock
    {
        [JsonPropertyName("captions")]
        public List<CaptionItem>? Captions { get; set; }
    }

    private sealed class CaptionItem
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("confidence")]
        public double Confidence { get; set; }
    }
}
