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
    public async Task DetectAsync_TruncatedJson_ThrowsAssetDetectionException()
    {
        var service = new NvidiaAssetDetectionService(
            new FakeVisionService("""{"objects": [{"label": "sofa", "confidence": 0.9"""));

        await Assert.ThrowsAsync<AssetDetectionException>(() => service.DetectAsync(Image, "image/jpeg"));
    }

    [Fact]
    public async Task DetectAsync_ProseWithNoJsonAtAll_ThrowsAssetDetectionException()
    {
        var service = new NvidiaAssetDetectionService(
            new FakeVisionService("I'm sorry, I can't identify objects in this image."));

        await Assert.ThrowsAsync<AssetDetectionException>(() => service.DetectAsync(Image, "image/jpeg"));
    }

    [Fact]
    public async Task DetectAsync_VisionServiceFails_ThrowsAssetDetectionException()
    {
        var service = new NvidiaAssetDetectionService(new ThrowingVisionService());

        await Assert.ThrowsAsync<AssetDetectionException>(() => service.DetectAsync(Image, "image/jpeg"));
    }
}
