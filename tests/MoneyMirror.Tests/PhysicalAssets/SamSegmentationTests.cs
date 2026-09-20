using Bunit;
using Microsoft.JSInterop;
using MoneyMirror.PhysicalAssets;

namespace MoneyMirror.Tests.PhysicalAssets;

public sealed class SamSegmentationTests : IDisposable
{
    private readonly BunitContext _context = new();
    private readonly BunitJSModuleInterop _module;
    private readonly BlazorSamSegmentationEngine _engine;

    public SamSegmentationTests()
    {
        _module = _context.JSInterop.SetupModule("./js/mobile-sam.js");
        _engine = new BlazorSamSegmentationEngine(_context.JSInterop.JSRuntime);
    }

    [Fact]
    public async Task Initialize_InvokesInitEngine_AndReturnsStatus()
    {
        var expected = new SamEngineStatus("ready", "webgpu", false, 1024, 768);
        _module.Setup<SamEngineStatus>("initEngine", _ => true).SetResult(expected);

        var status = await _engine.InitializeAsync();

        Assert.Equal("ready", status.State);
        Assert.Equal("webgpu", status.Device);
        Assert.False(status.HasEmbeddings);
        Assert.Equal(1024, status.Width);
        Assert.Equal(768, status.Height);
    }

    [Fact]
    public async Task GetStatus_InvokesGetEngineStatus()
    {
        var expected = new SamEngineStatus("ready", "wasm", true, 800, 600);
        _module.Setup<SamEngineStatus>("getEngineStatus", _ => true).SetResult(expected);

        var status = await _engine.GetStatusAsync();

        Assert.Equal("ready", status.State);
        Assert.Equal("wasm", status.Device);
        Assert.True(status.HasEmbeddings);
    }

    [Fact]
    public async Task DecodePoint_InvokesJsWithPromptTypeAndCoordinates()
    {
        var points = new List<NormalizedPoint>
        {
            new(0.2, 0.3),
            new(0.4, 0.3),
            new(0.4, 0.6),
            new(0.2, 0.6),
        };
        var bounds = new BoundingBox(0.2, 0.3, 0.2, 0.3);
        var expected = new SamSegmentationResult(points, bounds, 0.97, 100, 100, 14.2);

        _module
            .Setup<SamSegmentationResult>(
                "decodePoint",
                args =>
                {
                    var x = (double)args.Arguments[0]!;
                    var y = (double)args.Arguments[1]!;
                    var type = (int)args.Arguments[2]!;
                    return Math.Abs(x - 0.5) < 0.001 && Math.Abs(y - 0.5) < 0.001 && type == 1;
                }
            )
            .SetResult(expected);

        var result = await _engine.DecodePointAsync(0.5, 0.5, SamPromptType.Positive);

        Assert.Equal(4, result.Polygon.Count);
        Assert.Equal(0.97, result.Confidence);
        Assert.Equal(14.2, result.ElapsedMs);
    }

    [Fact]
    public async Task DecodePoints_InvokesJsWithMultiplePoints()
    {
        var points = new List<NormalizedPoint> { new(0.1, 0.1), new(0.5, 0.1), new(0.5, 0.5) };
        var bounds = new BoundingBox(0.1, 0.1, 0.4, 0.4);
        var expected = new SamSegmentationResult(points, bounds, 0.95, 100, 100, 18.5);

        _module.Setup<SamSegmentationResult>("decodeMultiPoints", _ => true).SetResult(expected);

        var prompts = new List<SamPointPrompt>
        {
            new(0.2, 0.2, SamPromptType.Positive),
            new(0.8, 0.8, SamPromptType.Negative),
        };

        var result = await _engine.DecodePointsAsync(prompts);

        Assert.Equal(3, result.Polygon.Count);
        Assert.Equal(0.95, result.Confidence);
    }

    [Fact]
    public async Task ResetPrior_InvokesResetPriorInModule()
    {
        var invocation = _module.SetupVoid("resetPrior", _ => true);
        invocation.SetVoidResult();

        await _engine.ResetPriorAsync();

        _module.VerifyInvoke("resetPrior", 1);
    }

    [Fact]
    public async Task Dispose_DisposesModuleAndInvokesDisposeEngine()
    {
        _module
            .Setup<SamEngineStatus>("getEngineStatus", _ => true)
            .SetResult(new SamEngineStatus("ready", "wasm", false, 0, 0));
        await _engine.GetStatusAsync();

        var disposeInvocation = _module.SetupVoid("disposeEngine", _ => true);
        disposeInvocation.SetVoidResult();

        await _engine.DisposeAsync();

        _module.VerifyInvoke("disposeEngine", 1);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
