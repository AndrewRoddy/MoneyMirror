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

        JsonException? lastError = null;

        foreach (var candidate in JsonObjectCandidates(completion))
        {
            try
            {
                if (JsonSerializer.Deserialize<DetectionResponse>(candidate, JsonOptions)
                    is { Objects: { } objects })
                {
                    return objects;
                }
            }
            catch (JsonException ex)
            {
                // Not the detection object - an earlier brace in the model's
                // commentary opened something else. Fall through to the next span.
                lastError = ex;
            }
        }

        const string message =
            "The vision model returned a response that could not be parsed as detected objects.";

        throw lastError is null
            ? new AssetDetectionException(message)
            : new AssetDetectionException(message, lastError);
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
              "identification": {"brand": string|null, "model": string|null, "confidence": number between 0 and 1} | null,
              "tags": [string]
            }
          ]
        }

        Rules:
        - One entry per distinct object found. A single-item photo still returns an array with one entry.
        - "region" gives the object's bounding box as fractions of image width/height (0-1); use null if you can't estimate it.
        - Only include "identification" when you can infer a specific brand or model with reasonable confidence.
          If you can't, set "identification" to null - never invent a specific product.
        - "tags" contains short, visible descriptors useful for lookup and filtering, such as color,
          material, pattern, or texture (for example: ["blue", "metal", "fuzzy"]). Do not use tags
          for an uncertain brand or model, and return an empty array when no useful descriptors are visible.
        - If no objects are found, return {"objects": []}.
        """;

    /// <summary>
    /// Yields every balanced <c>{...}</c> span in <paramref name="text"/>, outermost
    /// first. The model regularly ignores the "JSON only" instruction and precedes
    /// the object with a prose description, a markdown fence, or both, and can echo
    /// the schema from the prompt before answering - so callers try each span rather
    /// than assuming the whole response is one JSON object.
    /// </summary>
    private static IEnumerable<string> JsonObjectCandidates(string text)
    {
        for (var start = text.IndexOf('{'); start >= 0; start = text.IndexOf('{', start + 1))
        {
            if (ReadBalancedObject(text, start) is { } candidate)
            {
                yield return candidate;
            }
        }
    }

    /// <summary>
    /// Returns the <c>{...}</c> span starting at <paramref name="start"/>, or null if
    /// the braces never balance because the response was cut off mid-object. Braces
    /// inside string literals are ignored, so a label like "a {thing}" cannot end it.
    /// </summary>
    private static string? ReadBalancedObject(string text, int start)
    {
        var depth = 0;
        var inString = false;
        var escaped = false;

        for (var i = start; i < text.Length; i++)
        {
            var c = text[i];

            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (c == '\\')
                {
                    escaped = true;
                }
                else if (c == '"')
                {
                    inString = false;
                }

                continue;
            }

            switch (c)
            {
                case '"':
                    inString = true;
                    break;
                case '{':
                    depth++;
                    break;
                case '}':
                    depth--;
                    if (depth == 0)
                    {
                        return text[start..(i + 1)];
                    }

                    break;
            }
        }

        return null;
    }

    private record DetectionResponse(IReadOnlyList<DetectedAsset> Objects);
}
