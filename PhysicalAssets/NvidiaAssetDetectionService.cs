using System.Diagnostics.CodeAnalysis;
using System.Text;
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
        // Observed live against meta/llama-3.2-11b-vision-instruct: for the same
        // photo it honours "respond with ONLY a single JSON object" about four
        // runs in five, and otherwise answers in prose, or scatters the fields
        // across a markdown bullet list with no wrapper object. Both are recovered
        // by simply asking again with a blunter instruction, so a failed parse
        // costs one extra call rather than the whole scan.
        var completion = await RequestAsync(imageBytes, mediaType, Prompt, cancellationToken);
        if (TryParseDetections(completion, out var detections, out _))
        {
            return detections;
        }

        completion = await RequestAsync(imageBytes, mediaType, RetryPrompt, cancellationToken);
        if (TryParseDetections(completion, out detections, out var parseError))
        {
            return detections;
        }

        const string message =
            "The vision model returned a response that could not be parsed as detected objects.";

        throw parseError is null
            ? new AssetDetectionException(message) { RawResponse = completion }
            : new AssetDetectionException(message, parseError) { RawResponse = completion };
    }

    private async Task<string> RequestAsync(
        byte[] imageBytes,
        string mediaType,
        string prompt,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _visionService.DetectAsync(imageBytes, mediaType, prompt, cancellationToken);
        }
        catch (VisionServiceException ex)
        {
            throw new AssetDetectionException(
                "Failed to detect objects: the vision provider call failed.", ex);
        }
    }

    private static bool TryParseDetections(
        string completion,
        [NotNullWhen(true)] out IReadOnlyList<DetectedAsset>? detections,
        out JsonException? parseError)
    {
        parseError = null;

        foreach (var candidate in JsonObjectCandidates(completion))
        {
            try
            {
                if (JsonSerializer.Deserialize<DetectionResponse>(candidate, JsonOptions)
                    is { Objects: { } objects })
                {
                    detections = Normalize(objects);
                    return true;
                }
            }
            catch (JsonException ex)
            {
                // Not the detection object - an earlier brace in the model's
                // commentary opened something else. Fall through to the next span.
                parseError = ex;
            }
        }

        if (RepairBrackets(completion) is { } repaired)
        {
            try
            {
                if (JsonSerializer.Deserialize<DetectionResponse>(repaired, JsonOptions)
                    is { Objects: { } repairedObjects })
                {
                    detections = Normalize(repairedObjects);
                    return true;
                }
            }
            catch (JsonException ex)
            {
                parseError = ex;
            }
        }

        detections = null;
        return false;
    }

    /// <summary>
    /// Rebuilds the response's bracket structure from the first <c>{</c> onwards,
    /// closing each open bracket with the closer it actually needs. Observed live,
    /// both with <c>finish_reason: stop</c> so nothing was truncated: the model
    /// drops the final brace (<c>{"objects": [{...}]</c>) or closes with the wrong
    /// one (<c>{"objects": [{...}}}</c>). Only ever tried after every intact span
    /// has failed, and the result still has to deserialize, so a genuinely
    /// unparseable reply is not coerced into a bogus detection.
    /// </summary>
    private static string? RepairBrackets(string text)
    {
        var start = text.IndexOf('{');
        if (start < 0)
        {
            return null;
        }

        var builder = new StringBuilder();
        var expected = new Stack<char>();
        var inString = false;
        var escaped = false;

        for (var i = start; i < text.Length; i++)
        {
            var c = text[i];

            if (inString)
            {
                builder.Append(c);
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

            if (c is '}' or ']')
            {
                if (expected.Count == 0)
                {
                    break;
                }

                // Close with what is actually open, which repairs a mismatch.
                builder.Append(expected.Pop());
                if (expected.Count == 0)
                {
                    return builder.ToString();
                }

                continue;
            }

            builder.Append(c);
            switch (c)
            {
                case '"':
                    inString = true;
                    break;
                case '{':
                    expected.Push('}');
                    break;
                case '[':
                    expected.Push(']');
                    break;
            }
        }

        if (inString)
        {
            builder.Append('"');
        }

        while (expected.Count > 0)
        {
            builder.Append(expected.Pop());
        }

        return builder.ToString();
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
    /// Second attempt after a failed parse. Kept as a suffix rather than a
    /// rewrite so the schema and rules stay identical - only the insistence
    /// changes. Written without literal braces so it composes as a const.
    /// </summary>
    private const string RetrySuffix = """

        Your previous reply was rejected because it was not a single JSON object.
        Do not describe the image. Do not use markdown, bullet points, or headings.
        Do not split the fields across separate objects. Reply with the one JSON
        object and nothing else: the first character must be an opening brace and
        the last must be a closing brace.
        """;

    private const string RetryPrompt = Prompt + RetrySuffix;

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

    // The model routinely omits "tags" (or sends it as null) even though the
    // prompt asks for an array. DetectedAsset.Tags is typed non-nullable, but
    // System.Text.Json does not enforce that - it just assigns null - and the
    // review UI dereferences Tags.Count while rendering. An unhandled exception
    // there kills the Blazor circuit, which leaves the scan stuck on its
    // spinner, so normalize here and let every caller trust the shape.
    private static IReadOnlyList<DetectedAsset> Normalize(IReadOnlyList<DetectedAsset> objects) =>
        [.. objects
            .Where(detected => detected is not null)
            .Select(detected => detected.Tags is null ? detected with { Tags = [] } : detected)];

    private record DetectionResponse(IReadOnlyList<DetectedAsset> Objects);
}
