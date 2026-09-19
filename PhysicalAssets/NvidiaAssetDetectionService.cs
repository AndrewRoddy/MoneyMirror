using System.Text.Json;
using MoneyMirror.Ai;

namespace MoneyMirror.PhysicalAssets;

/// <summary>
/// <see cref="IPhysicalAssetDetectionService"/> implementation: prompts the
/// vision model (via <see cref="IVisionService"/>) for a JSON object listing
/// each detected object and parses the response. Structured output is
/// enforced through the prompt plus strict JSON parsing, since
/// <see cref="IVisionService"/> exposes plain image-in/text-out.
/// </summary>
public class NvidiaAssetDetectionService : IPhysicalAssetDetectionService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IVisionService _visionService;

    public NvidiaAssetDetectionService(IVisionService visionService)
    {
        _visionService = visionService;
    }

    public async Task<IReadOnlyList<DetectedAsset>> DetectAsync(
        byte[] imageBytes,
        string mediaType,
        CancellationToken cancellationToken = default)
    {
        string completion;
        try
        {
            completion = await _visionService.DetectAsync(imageBytes, mediaType, Prompt, cancellationToken);
        }
        catch (VisionServiceException ex)
        {
            throw new AssetDetectionException(
                "Failed to detect objects: the vision provider call failed.", ex);
        }

        var json = StripCodeFence(completion);

        try
        {
            var result = JsonSerializer.Deserialize<DetectionResponse>(json, JsonOptions);
            return result?.Objects ?? [];
        }
        catch (JsonException ex)
        {
            throw new AssetDetectionException(
                "The vision model returned a response that could not be parsed as detected objects.", ex);
        }
    }

    private const string Prompt = """
        You detect distinct physical objects/possessions in a photo. The photo may
        show a single item or a room with several possessions. Respond with ONLY a
        single JSON object - no markdown code fences, no commentary - matching
        exactly this shape:

        {
          "objects": [
            {
              "label": string,
              "confidence": number between 0 and 1,
              "region": {"x": number, "y": number, "width": number, "height": number} | null,
              "identification": {"brand": string|null, "model": string|null, "confidence": number between 0 and 1} | null
            }
          ]
        }

        Rules:
        - One entry per distinct object found. A single-item photo still returns an array with one entry.
        - "region" gives the object's bounding box as fractions of image width/height (0-1); use null if you can't estimate it.
        - Only include "identification" when you can infer a specific brand or model with reasonable confidence.
          If you can't, set "identification" to null - never invent a specific product.
        - If no objects are found, return {"objects": []}.
        """;

    private static string StripCodeFence(string text)
    {
        var trimmed = text.Trim();
        if (!trimmed.StartsWith("```"))
        {
            return trimmed;
        }

        var firstNewline = trimmed.IndexOf('\n');
        if (firstNewline >= 0)
        {
            trimmed = trimmed[(firstNewline + 1)..];
        }

        var closingFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
        if (closingFence >= 0)
        {
            trimmed = trimmed[..closingFence];
        }

        return trimmed.Trim();
    }

    private record DetectionResponse(IReadOnlyList<DetectedAsset> Objects);
}

