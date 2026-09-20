using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using MoneyMirror.Features.PhysicalAssets;
using MoneyMirror.PhysicalAssets;

namespace MoneyMirror.Tests.PhysicalAssets;

public sealed class LiveCameraScannerTests : IDisposable
{
    private readonly BunitContext _context = new();
    private readonly BunitJSModuleInterop _module;
    private readonly FakeSamEngine _samEngine = new();
    private readonly FakeStorage _storage = new();

    public LiveCameraScannerTests()
    {
        _context.Services.AddLogging();
        _context.Services.AddSingleton<ISamSegmentationEngine>(_samEngine);
        _context.Services.AddSingleton<IPossessionImageStorage>(_storage);

        _module = _context.JSInterop.SetupModule("./js/camera-scanner.js");
        _module
            .Setup<CameraSupport>("describeSupport", _ => true)
            .SetResult(new CameraSupport(SecureContext: true, HasMediaDevices: true, IsIos: true));
        _module
            .Setup<CameraStartResult>("openCamera", _ => true)
            .SetResult(new CameraStartResult(Ok: true, Facing: "environment", null, null));
        _module
            .Setup<CameraStartResult>("flipCamera", _ => true)
            .SetResult(new CameraStartResult(Ok: true, Facing: "user", null, null));
        _module.SetupVoid("stopCamera", _ => true).SetVoidResult();
        _module.SetupVoid("freezeFrame", _ => true).SetVoidResult();
        _module.SetupVoid("unfreezeFrame", _ => true).SetVoidResult();
        _module
            .Setup<NormalizedPoint>("normalizePoint", _ => true)
            .SetResult(new NormalizedPoint(0.5, 0.5));
        _module.Setup<IJSStreamReference>("getCutoutBlob", _ => true).SetResult(new FakeStream());
        _module.Setup<IJSStreamReference>("getCanvasBlob", _ => true).SetResult(new FakeStream());
    }

    [Fact]
    public void RendersInactivePlaceholderInitially()
    {
        var cut = _context.Render<LiveCameraScanner>();

        Assert.Contains("Start live camera", cut.Markup);
        Assert.DoesNotContain("glowing-contour", cut.Markup);
    }

    [Fact]
    public async Task StartCamera_InitializesEngineAndStream()
    {
        var cut = _context.Render<LiveCameraScanner>();

        await cut.Find("button.btn-primary").ClickAsync(new MouseEventArgs());

        _module.VerifyInvoke("openCamera", 1);
        Assert.True(_samEngine.InitCalls >= 1);
        Assert.Contains("Camera active", cut.Markup);
    }

    [Fact]
    public async Task Tap_FreezesAndSegmentsFrame()
    {
        var cut = _context.Render<LiveCameraScanner>();
        await cut.Find("button.btn-primary").ClickAsync(new MouseEventArgs());

        await cut.Find(".viewfinder-stage")
            .ClickAsync(new MouseEventArgs { ClientX = 200, ClientY = 150 });

        _module.VerifyInvoke("freezeFrame", 1);
        Assert.Equal(1, _samEngine.EncodeCalls);
        Assert.Equal(1, _samEngine.DecodePointCalls);
        Assert.Contains("glowing-contour", cut.Markup);
        Assert.Contains("prompt-marker", cut.Markup);
    }

    [Fact]
    public async Task RefinementTap_CallsDecodePoints()
    {
        var cut = _context.Render<LiveCameraScanner>();
        await cut.Find("button.btn-primary").ClickAsync(new MouseEventArgs());

        await cut.Find(".viewfinder-stage")
            .ClickAsync(new MouseEventArgs { ClientX = 200, ClientY = 150 });
        await cut.Find(".viewfinder-stage")
            .ClickAsync(new MouseEventArgs { ClientX = 220, ClientY = 170 });

        Assert.Equal(1, _samEngine.DecodePointsCalls);
    }

    [Fact]
    public async Task SwitchPromptMode_TogglesMode()
    {
        var cut = _context.Render<LiveCameraScanner>();
        await cut.Find("button.btn-primary").ClickAsync(new MouseEventArgs());
        await cut.Find(".viewfinder-stage")
            .ClickAsync(new MouseEventArgs { ClientX = 200, ClientY = 150 });

        var excludeButton = cut.FindAll("button.mode-btn")
            .Single(b => b.TextContent.Contains("Exclude"));
        await excludeButton.ClickAsync(new MouseEventArgs());

        var updatedButton = cut.FindAll("button.mode-btn")
            .Single(b => b.TextContent.Contains("Exclude"));
        Assert.Contains("active negative", updatedButton.ClassName);
    }

    [Fact]
    public async Task Retap_ResetsFrameAndPrior()
    {
        var cut = _context.Render<LiveCameraScanner>();
        await cut.Find("button.btn-primary").ClickAsync(new MouseEventArgs());
        await cut.Find(".viewfinder-stage")
            .ClickAsync(new MouseEventArgs { ClientX = 200, ClientY = 150 });

        var retapButton = cut.FindAll("button").Single(b => b.TextContent.Contains("Retap"));
        await retapButton.ClickAsync(new MouseEventArgs());

        _module.VerifyInvoke("unfreezeFrame", 1);
        Assert.Equal(1, _samEngine.ResetCalls);
        Assert.DoesNotContain("glowing-contour", cut.Markup);
    }

    [Fact]
    public async Task SwitchCamera_CallsJsSwitchCamera()
    {
        var cut = _context.Render<LiveCameraScanner>();
        await cut.Find("button.btn-primary").ClickAsync(new MouseEventArgs());

        var switchButton = cut.FindAll("button")
            .Single(b => b.TextContent.Contains("Switch camera"));
        await switchButton.ClickAsync(new MouseEventArgs());

        _module.VerifyInvoke("flipCamera", 1);
    }

    [Fact]
    public async Task CameraRefused_ExplainsHowToGrantItBack()
    {
        using var context = new BunitContext();
        context.Services.AddLogging();
        context.Services.AddSingleton<ISamSegmentationEngine>(new FakeSamEngine());
        context.Services.AddSingleton<IPossessionImageStorage>(new FakeStorage());

        var module = context.JSInterop.SetupModule("./js/camera-scanner.js");
        module.SetupVoid("stopCamera", _ => true).SetVoidResult();
        module
            .Setup<CameraSupport>("describeSupport", _ => true)
            .SetResult(new CameraSupport(SecureContext: true, HasMediaDevices: true, IsIos: true));
        module
            .Setup<CameraStartResult>("openCamera", _ => true)
            .SetResult(
                new CameraStartResult(
                    Ok: false,
                    Facing: null,
                    Code: "NotAllowedError",
                    Message: "Camera access was denied."
                )
            );

        var cut = context.Render<LiveCameraScanner>();
        await cut.Find("button.btn-primary").ClickAsync(new MouseEventArgs());

        // Safari refuses again without prompting once it has been told no, so a
        // second tap cannot help and the way back has to be spelled out.
        Assert.Contains("Camera access was denied.", cut.Markup);
        Assert.Contains("Website Settings", cut.Markup);
        Assert.DoesNotContain("Camera active", cut.Markup);
    }

    [Fact]
    public void InsecureOrigin_OffersNoCameraButtonAtAll()
    {
        using var context = new BunitContext();
        context.Services.AddLogging();
        context.Services.AddSingleton<ISamSegmentationEngine>(new FakeSamEngine());
        context.Services.AddSingleton<IPossessionImageStorage>(new FakeStorage());

        var module = context.JSInterop.SetupModule("./js/camera-scanner.js");
        module.SetupVoid("stopCamera", _ => true).SetVoidResult();
        module
            .Setup<CameraSupport>("describeSupport", _ => true)
            .SetResult(
                new CameraSupport(SecureContext: false, HasMediaDevices: false, IsIos: true)
            );

        var cut = context.Render<LiveCameraScanner>();

        // There is no camera API on a plain http:// origin, so a button could do
        // nothing but fail.
        Assert.DoesNotContain("Start live camera", cut.Markup);
        Assert.Contains("insecure connection", cut.Markup);
    }

    [Fact]
    public async Task AcceptItem_SavesImageAndEmitsEvent()
    {
        LiveScannedItem? acceptedItem = null;
        var cut = _context.Render<LiveCameraScanner>(parameters =>
            parameters.Add(p => p.OnItemAccepted, (LiveScannedItem item) => acceptedItem = item)
        );

        await cut.Find("button.btn-primary").ClickAsync(new MouseEventArgs());
        await cut.Find(".viewfinder-stage")
            .ClickAsync(new MouseEventArgs { ClientX = 200, ClientY = 150 });

        var acceptButton = cut.FindAll("button").Single(b => b.TextContent.Contains("Accept item"));
        await acceptButton.ClickAsync(new MouseEventArgs());

        Assert.NotNull(acceptedItem);
        Assert.True(acceptedItem.Mask.Count >= 3);
        Assert.NotNull(acceptedItem.CutoutBytes);
        Assert.NotEmpty(acceptedItem.CutoutBytes);
        Assert.Equal(1, _storage.Calls);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    private sealed class FakeStream : IJSStreamReference
    {
        public long Length => 3;

        public ValueTask<Stream> OpenReadStreamAsync(
            long maxAllowedSize = 512000,
            CancellationToken cancellationToken = default
        ) => ValueTask.FromResult<Stream>(new MemoryStream([1, 2, 3]));

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeStorage : IPossessionImageStorage
    {
        public List<string> References { get; } = [];
        public int Calls { get; private set; }

        public Task<string> SaveAsync(
            Stream content,
            string fileName,
            CancellationToken cancellationToken = default
        )
        {
            Calls++;
            var reference = $"live-scan-{Calls}.jpg";
            References.Add(reference);
            return Task.FromResult(reference);
        }

        public Task<Stream?> OpenReadAsync(
            string reference,
            CancellationToken cancellationToken = default
        ) => Task.FromResult<Stream?>(null);
    }
}
