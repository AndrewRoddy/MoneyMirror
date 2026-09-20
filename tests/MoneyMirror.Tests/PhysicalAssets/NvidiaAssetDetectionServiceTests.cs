using MoneyMirror.Ai;
using MoneyMirror.PhysicalAssets;

namespace MoneyMirror.Tests.PhysicalAssets;

public class NvidiaAssetDetectionServiceTests
{
    private class FakeVisionService(string response) : IVisionService
    {
        public Task<string> DetectAsync(
            byte[] imageBytes, string mediaType, string prompt, CancellationToken cancellationToken = default) =>
            Task.FromResult(response);
    }

    private class SequencedVisionService(params string[] responses) : IVisionService
    {
        private readonly List<string> _prompts = [];

        public IReadOnlyList<string> Prompts => _prompts;

        public Task<string> DetectAsync(
            byte[] imageBytes, string mediaType, string prompt, CancellationToken cancellationToken = default)
        {
            var response = responses[Math.Min(_prompts.Count, responses.Length - 1)];
            _prompts.Add(prompt);
            return Task.FromResult(response);
        }
    }

    private class ThrowingVisionService : IVisionService
    {
        public Task<string> DetectAsync(
            byte[] imageBytes, string mediaType, string prompt, CancellationToken cancellationToken = default) =>
            throw new VisionServiceException("provider unavailable");
    }

    private static readonly byte[] Image = [1, 2, 3];

    private const string Fence = "```";

    private const string SingleObjectJson = """
        {
          "objects": [
            {
              "label": "acoustic guitar",
              "confidence": 0.95,
              "region": {"x": 0.1, "y": 0.1, "width": 0.5, "height": 0.8},
              "identification": {"brand": "Fender", "model": "CD-60S", "confidence": 0.7},
              "tags": ["brown", "wood", "stringed"]
            }
          ]
        }
        """;

    private const string MultiObjectJson = """
        {
          "objects": [
            {"label": "sofa", "confidence": 0.9, "region": null, "identification": null, "tags": ["blue", "fabric"]},
            {"label": "lamp", "confidence": 0.8, "region": null, "identification": null, "tags": ["metal"]},
            {"label": "coffee table", "confidence": 0.85, "region": null, "identification": null, "tags": ["wood", "brown"]}
          ]
        }
        """;

    private const string LowConfidenceJson = """
        {
          "objects": [
            {"label": "power tool", "confidence": 0.6, "region": null, "identification": null, "tags": []}
          ]
        }
        """;

    private const string MissingTagsJson = """
        {
          "objects": [
            {"label": "desk chair", "confidence": 0.9, "region": null, "identification": null}
          ]
        }
        """;

    private const string NullTagsJson = """
        {
          "objects": [
            {"label": "desk chair", "confidence": 0.9, "region": null, "identification": null, "tags": null}
          ]
        }
        """;

    // The review UI dereferences Tags.Count while rendering, so a null here
    // takes down the Blazor circuit and strands the scan on its spinner.
    [Fact]
    public async Task DetectAsync_ObjectWithoutTagsField_ReturnsEmptyTagsNotNull()
    {
        var service = new NvidiaAssetDetectionService(new FakeVisionService(MissingTagsJson));

        var results = await service.DetectAsync(Image, "image/jpeg");

        var asset = Assert.Single(results);
        Assert.NotNull(asset.Tags);
        Assert.Empty(asset.Tags);
    }

    [Fact]
    public async Task DetectAsync_ObjectWithExplicitNullTags_ReturnsEmptyTagsNotNull()
    {
        var service = new NvidiaAssetDetectionService(new FakeVisionService(NullTagsJson));

        var results = await service.DetectAsync(Image, "image/jpeg");

        var asset = Assert.Single(results);
        Assert.NotNull(asset.Tags);
        Assert.Empty(asset.Tags);
    }

    [Fact]
    public async Task DetectAsync_SingleObjectWithIdentification_ReturnsOneDetectedAssetWithIdentification()
    {
        var service = new NvidiaAssetDetectionService(new FakeVisionService(SingleObjectJson));

        var results = await service.DetectAsync(Image, "image/jpeg");

        var asset = Assert.Single(results);
        Assert.Equal("acoustic guitar", asset.Label);
        Assert.NotNull(asset.Identification);
        Assert.Equal("Fender", asset.Identification!.Brand);
        Assert.Equal(["brown", "wood", "stringed"], asset.Tags);
        Assert.NotNull(asset.Region);
    }

    [Fact]
    public async Task DetectAsync_MultiObjectPhoto_ReturnsOneEntryPerObject()
    {
        var service = new NvidiaAssetDetectionService(new FakeVisionService(MultiObjectJson));

        var results = await service.DetectAsync(Image, "image/jpeg");

        Assert.Equal(3, results.Count);
        Assert.Contains(results, a => a.Label == "sofa");
        Assert.Contains(results, a => a.Label == "lamp");
        Assert.Contains(results, a => a.Label == "coffee table");
        Assert.Equal(["blue", "fabric"], results.Single(a => a.Label == "sofa").Tags);
    }

    [Fact]
    public async Task DetectAsync_LowConfidenceIdentification_DoesNotFabricateProduct()
    {
        var service = new NvidiaAssetDetectionService(new FakeVisionService(LowConfidenceJson));

        var results = await service.DetectAsync(Image, "image/jpeg");

        var asset = Assert.Single(results);
        Assert.Null(asset.Identification);
    }

    [Fact]
    public async Task DetectAsync_NoObjectsFound_ReturnsEmptyList()
    {
        var service = new NvidiaAssetDetectionService(new FakeVisionService("""{"objects": []}"""));

        var results = await service.DetectAsync(Image, "image/jpeg");

        Assert.Empty(results);
    }

    [Fact]
    public async Task DetectAsync_MalformedJson_ThrowsAssetDetectionException()
    {
        var service = new NvidiaAssetDetectionService(new FakeVisionService("not json at all"));

        await Assert.ThrowsAsync<AssetDetectionException>(() => service.DetectAsync(Image, "image/jpeg"));
    }

    // #(scan) The configured vision model routinely ignores "respond with ONLY a
    // single JSON object". These are response shapes observed from
    // meta/llama-3.2-11b-vision-instruct for real scanned photos.
    [Fact]
    public async Task DetectAsync_ProsePrecedingJson_ReturnsDetectedAssets()
    {
        const string response = """
            The image shows a Minion from the Despicable Me franchise. The Minion is
            wearing goggles, a denim overall, and gloves.

            Here is the JSON object describing the Minion:

            { "objects": [ { "label": "Minion", "confidence": 1, "region": null, "identification": null, "tags": ["yellow"] } ] }
            """;
        var service = new NvidiaAssetDetectionService(new FakeVisionService(response));

        var results = await service.DetectAsync(Image, "image/jpeg");

        Assert.Equal("Minion", Assert.Single(results).Label);
    }

    [Fact]
    public async Task DetectAsync_JsonInMarkdownFence_ReturnsDetectedAssets()
    {
        var response = "Sure! Here you go:\n\n" + Fence + "json\n" + SingleObjectJson + "\n" + Fence;
        var service = new NvidiaAssetDetectionService(new FakeVisionService(response));

        var results = await service.DetectAsync(Image, "image/jpeg");

        Assert.Equal("acoustic guitar", Assert.Single(results).Label);
    }

    [Fact]
    public async Task DetectAsync_TrailingCommentaryAfterJson_ReturnsDetectedAssets()
    {
        var service = new NvidiaAssetDetectionService(
            new FakeVisionService(SingleObjectJson + "\n\nLet me know if you want more detail!"));

        var results = await service.DetectAsync(Image, "image/jpeg");

        Assert.Equal("acoustic guitar", Assert.Single(results).Label);
    }

    // The model sometimes restates the schema from the prompt before answering.
    // That block is not valid JSON, so the real object after it must still win.
    [Fact]
    public async Task DetectAsync_SchemaEchoBeforeJson_ReturnsDetectedAssets()
    {
        const string response = """
            I will match this shape:
            {"objects": [{"label": string, "confidence": number, "region": null, "identification": null, "tags": [string]}]}

            {"objects": [{"label": "sofa", "confidence": 0.9, "region": null, "identification": null, "tags": []}]}
            """;
        var service = new NvidiaAssetDetectionService(new FakeVisionService(response));

        var results = await service.DetectAsync(Image, "image/jpeg");

        Assert.Equal("sofa", Assert.Single(results).Label);
    }

    // A brace inside a label must not be mistaken for the end of the object.
    [Fact]
    public async Task DetectAsync_BraceInsideStringValue_ReturnsDetectedAssets()
    {
        const string response = """
            Here it is: {"objects": [{"label": "sign reading {open}", "confidence": 0.5, "region": null, "identification": null, "tags": []}]}
            """;
        var service = new NvidiaAssetDetectionService(new FakeVisionService(response));

        var results = await service.DetectAsync(Image, "image/jpeg");

        Assert.Equal("sign reading {open}", Assert.Single(results).Label);
    }

    // A response cut off mid-object has no balanced span, so it must surface as a
    // detection failure rather than being reported as zero objects found.
    [Fact]
    // Bracket repair deliberately changed this: a response cut off mid-object is
    // now completed and the objects that did arrive are kept, rather than the
    // whole scan failing. Fields the model never got to send stay null/empty.
    public async Task DetectAsync_TruncatedJson_RecoversTheObjectsThatArrived()
    {
        var service = new NvidiaAssetDetectionService(
            new FakeVisionService("""{"objects": [{"label": "sofa", "confidence": 0.9"""));

        var asset = Assert.Single(await service.DetectAsync(Image, "image/jpeg"));

        Assert.Equal("sofa", asset.Label);
        Assert.Equal(0.9, asset.Confidence);
        Assert.Null(asset.Region);
        Assert.Empty(asset.Tags);
    }

    [Fact]
    public async Task DetectAsync_ProseWithNoJsonAtAll_ThrowsAssetDetectionException()
    {
        var service = new NvidiaAssetDetectionService(
            new FakeVisionService("I'm sorry, I can't identify objects in this image."));

        await Assert.ThrowsAsync<AssetDetectionException>(() => service.DetectAsync(Image, "image/jpeg"));
    }

    // Observed live: for the same photo the model answers with a bare prose
    // description roughly one run in five, with finish_reason "stop" and no
    // JSON anywhere. The scan UI offers the raw reply behind a button so this
    // is distinguishable from a parser fault without reading server logs.
    [Fact]
    public async Task DetectAsync_WhenResponseCannotBeParsed_CarriesTheRawResponse()
    {
        const string prose = "The image shows a water bottle with a screw-on lid and a handle on top.";
        var service = new NvidiaAssetDetectionService(new FakeVisionService(prose));

        var ex = await Assert.ThrowsAsync<AssetDetectionException>(
            () => service.DetectAsync(Image, "image/jpeg"));

        Assert.Equal(prose, ex.RawResponse);
    }

    [Fact]
    public async Task DetectAsync_WhenProviderCallFails_HasNoRawResponse()
    {
        var service = new NvidiaAssetDetectionService(new ThrowingVisionService());

        var ex = await Assert.ThrowsAsync<AssetDetectionException>(
            () => service.DetectAsync(Image, "image/jpeg"));

        Assert.Null(ex.RawResponse);
    }

    [Fact]
    public async Task DetectAsync_VisionServiceFails_ThrowsAssetDetectionException()
    {
        var service = new NvidiaAssetDetectionService(new ThrowingVisionService());

        await Assert.ThrowsAsync<AssetDetectionException>(() => service.DetectAsync(Image, "image/jpeg"));
    }

    // The markdown-bullet shape observed live: every field is present, but the
    // wrapper object is missing, so the balanced-brace scan finds only the
    // "region" and "identification" fragments and neither is a DetectionResponse.
    private const string BulletListResponse = """
        The image shows a water bottle.

        *   **Label:** Water Bottle
        *   **Confidence:** 1.0
        *   **Region:** { "x": 0.1, "y": 0.2, "width": 0.8, "height": 0.6 }
        *   **Identification:** { "brand": "OXO", "model": null, "confidence": 1.0 }
        *   **Tags:** ["metallic", "gray", "shiny"]
        """;

    [Fact]
    public async Task DetectAsync_WhenFirstReplyIsProse_RetriesAndReturnsDetections()
    {
        var vision = new SequencedVisionService(
            "The image shows a water bottle with a screw-on lid.",
            SingleObjectJson);
        var service = new NvidiaAssetDetectionService(vision);

        var results = await service.DetectAsync(Image, "image/jpeg");

        Assert.Equal("acoustic guitar", Assert.Single(results).Label);
        Assert.Equal(2, vision.Prompts.Count);
    }

    [Fact]
    public async Task DetectAsync_WhenFirstReplyIsAMarkdownBulletList_RetriesAndReturnsDetections()
    {
        var vision = new SequencedVisionService(BulletListResponse, SingleObjectJson);
        var service = new NvidiaAssetDetectionService(vision);

        var results = await service.DetectAsync(Image, "image/jpeg");

        Assert.Single(results);
        Assert.Equal(2, vision.Prompts.Count);
    }

    [Fact]
    public async Task DetectAsync_RetryPromptRestatesTheJsonOnlyRequirement()
    {
        var vision = new SequencedVisionService("no json here", SingleObjectJson);
        var service = new NvidiaAssetDetectionService(vision);

        await service.DetectAsync(Image, "image/jpeg");

        Assert.DoesNotContain("previous reply was rejected", vision.Prompts[0]);
        Assert.Contains("previous reply was rejected", vision.Prompts[1]);
    }

    [Fact]
    public async Task DetectAsync_WhenFirstReplyParses_DoesNotRetry()
    {
        var vision = new SequencedVisionService(SingleObjectJson);
        var service = new NvidiaAssetDetectionService(vision);

        await service.DetectAsync(Image, "image/jpeg");

        Assert.Single(vision.Prompts);
    }

    [Fact]
    public async Task DetectAsync_WhenBothRepliesFail_RawResponseIsTheRetryReply()
    {
        var vision = new SequencedVisionService("first prose reply", "second prose reply");
        var service = new NvidiaAssetDetectionService(vision);

        var ex = await Assert.ThrowsAsync<AssetDetectionException>(
            () => service.DetectAsync(Image, "image/jpeg"));

        Assert.Equal("second prose reply", ex.RawResponse);
        Assert.Equal(2, vision.Prompts.Count);
    }

    // Both captured verbatim from meta/llama-3.2-11b-vision-instruct, each with
    // finish_reason "stop" - the model simply got the brackets wrong. Everything
    // needed is present, so these are repaired rather than spending another call.
    private const string MissingFinalBrace =
        """{"objects": [{"label": "water bottle", "confidence": 1, "region": {"x": 0.1, "y": 0.1, "width": 0.8, "height": 0.9}, "identification": {"brand": "OXO", "model": null, "confidence": 1}, "tags": ["blue", "metal"]}]""";

    private const string MismatchedClosers =
        """{"objects": [{"label":"bottle","confidence":1,"region":{"x":0.234,"y":0.151,"width":0.255,"height":0.744},"identification":{"brand":"OXO","model":null,"confidence":1},"tags":["blue","metal"]}}}""";

    [Fact]
    public async Task DetectAsync_ResponseMissingItsFinalBrace_ReturnsDetectedAssets()
    {
        var vision = new SequencedVisionService(MissingFinalBrace);
        var service = new NvidiaAssetDetectionService(vision);

        var asset = Assert.Single(await service.DetectAsync(Image, "image/jpeg"));

        Assert.Equal("water bottle", asset.Label);
        Assert.Equal("OXO", asset.Identification!.Brand);
        Assert.Equal(["blue", "metal"], asset.Tags);
        Assert.Single(vision.Prompts);
    }

    [Fact]
    public async Task DetectAsync_ResponseClosingWithTheWrongBrackets_ReturnsDetectedAssets()
    {
        var vision = new SequencedVisionService(MismatchedClosers);
        var service = new NvidiaAssetDetectionService(vision);

        var asset = Assert.Single(await service.DetectAsync(Image, "image/jpeg"));

        Assert.Equal("bottle", asset.Label);
        Assert.NotNull(asset.Region);
        Assert.Single(vision.Prompts);
    }

    // The repair must not manufacture a detection out of a reply that never
    // contained one - the result still has to deserialize into the schema.
    [Fact]
    public async Task DetectAsync_BracketRepairDoesNotSalvageAnUnrelatedObject()
    {
        var vision = new SequencedVisionService(
            """Here is the region I found: {"x": 0.1, "y": 0.2, "width": 0.8""");
        var service = new NvidiaAssetDetectionService(vision);

        await Assert.ThrowsAsync<AssetDetectionException>(
            () => service.DetectAsync(Image, "image/jpeg"));
    }

    [Fact]
    public async Task DetectAsync_BracketInsideAStringValueIsNotTreatedAsStructure()
    {
        var vision = new SequencedVisionService(
            """{"objects": [{"label": "sign reading {closed}", "confidence": 0.9, "region": null, "identification": null, "tags": []}]}""");
        var service = new NvidiaAssetDetectionService(vision);

        var asset = Assert.Single(await service.DetectAsync(Image, "image/jpeg"));

        Assert.Equal("sign reading {closed}", asset.Label);
    }
}
