using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using MoneyMirror.Data;
using MoneyMirror.Features.PhysicalAssets;
using MoneyMirror.PhysicalAssets;
using ScanPage = MoneyMirror.Features.PhysicalAssets.PhysicalAssets;

namespace MoneyMirror.Tests.PhysicalAssets;

/// <summary>Real rendered components and SQLite repository; only browser streams and AI are faked.</summary>
public sealed class VideoScanPickerTests : IDisposable
{
    private readonly BunitContext _context = new();
    private readonly SqliteConnection _connection = new("Filename=:memory:");
    private readonly MoneyMirrorDbContext _db;
    private readonly EfPhysicalAssetRepository _repository;
    private readonly FakeDetector _detector = new();
    private readonly FakeStorage _storage = new();
    private readonly FakeValuation _valuation = new();
    private readonly BunitJSModuleInterop _module;

    public VideoScanPickerTests()
    {
        _connection.Open();
        _db = new MoneyMirrorDbContext(
            new DbContextOptionsBuilder<MoneyMirrorDbContext>().UseSqlite(_connection).Options
        );
        _db.Database.EnsureCreated();
        _repository = new EfPhysicalAssetRepository(_db);
        _context.Services.AddLogging();
        _context.Services.AddOptions<ImageUploadOptions>();
        _context.Services.AddSingleton<IImageUploadValidator, ImageUploadValidator>();
        _context.Services.AddSingleton<IPhysicalAssetDetectionService>(_detector);
        _context.Services.AddSingleton<IPossessionImageStorage>(_storage);
        _context.Services.AddSingleton<IAssetValuationService>(_valuation);
        _context.Services.AddSingleton<IPhysicalAssetRepository>(_repository);
        _context.Services.AddSingleton<ISamSegmentationEngine>(new FakeSamEngine());
        _module = _context.JSInterop.SetupModule("./js/video-scan.js");
        _module.Setup<double[]>("prepare", _ => true).SetResult([0.5, 1.5]);
        _module.Setup<string>("frameUrl", _ => true).SetResult("blob:test-frame");
        _module.Setup<IJSStreamReference>("frameStream", _ => true).SetResult(new FakeStream());
        _module.Setup<IJSStreamReference>("cropStream", _ => true).SetResult(new FakeStream());
        _module.SetupVoid("dispose", _ => true).SetVoidResult();
    }

    [Fact]
    public async Task SelectedItemsOnly_GetSeparateCrops_AndPersistThroughExistingReview()
    {
        var page = _context.Render<ScanPage>(p => p.Add(x => x.ShowVideoScan, true));
        page.Find("#room-video").Change("room.mp4");
        await page.FindAll("button")
            .Single(b => b.TextContent == "Find items in video")
            .ClickAsync(new MouseEventArgs());
        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll(".item-region").Count));
        Assert.Empty(_storage.References);
        Assert.Empty(await _repository.GetAllAsync());

        // Two adjacent sightings of each item share selection. No items start selected.
        Assert.All(
            page.FindAll(".item-region"),
            b => Assert.Equal("false", b.GetAttribute("aria-pressed"))
        );
        page.FindAll(".item-region")[0].Click();
        page.FindAll(".item-region")[1].Click();
        page.FindAll("button").Single(b => b.TextContent.Trim() == "1.5 s").Click();
        Assert.All(
            page.FindAll(".item-region"),
            b => Assert.Equal("true", b.GetAttribute("aria-pressed"))
        );
        await page.FindAll("button")
            .Single(b => b.TextContent.Trim() == "Review selected items")
            .ClickAsync(new MouseEventArgs());
        page.WaitForAssertion(() =>
            Assert.Equal(2, page.FindAll("img[src^='/api/possession-images/']").Count)
        );
        Assert.Equal(2, _storage.References.Count);
        Assert.Equal(2, _module.Invocations.Count(i => i.Identifier == "cropStream"));
        Assert.Empty(await _repository.GetAllAsync()); // Reviewing is not inventory consent.

        page.WaitForAssertion(() =>
            Assert.Equal(
                2,
                page.FindAll("button").Count(b => b.TextContent.Trim() == "Save as new item")
            )
        );

        for (var i = 0; i < 2; i++)
        {
            var saveButton = page.FindAll("button")
                .First(b => b.TextContent.Trim() == "Save as new item");
            Assert.False(saveButton.HasAttribute("disabled"), page.Markup);
            await saveButton.ClickAsync(new MouseEventArgs());
            Assert.DoesNotContain("Failed to save to inventory:", page.Markup);
            Assert.Equal(i + 1, (await _repository.GetAllAsync()).Count);
            page.WaitForAssertion(() =>
                Assert.Equal(
                    i + 1,
                    page.FindAll(".alert-success")
                        .Count(a => a.TextContent.Contains("Saved to inventory."))
                )
            );
        }

        var saved = await _repository.GetAllAsync();
        Assert.Equal(2, saved.Count);
        var details = await Task.WhenAll(saved.Select(s => _repository.GetByIdAsync(s.Id)));
        Assert.Equal(_storage.References.Order(), details.Select(d => d!.ImageReference).Order());
    }

    [Fact]
    public void UnselectedItems_AreNotCroppedOrPassedToReview()
    {
        IReadOnlyList<SelectedVideoAsset>? reviewed = null;
        var picker = _context.Render<VideoScanPicker>(p =>
            p.Add(c => c.OnSelected, items => reviewed = items)
        );
        picker.Find("#room-video").Change("room.mp4");
        picker.Find("button").Click();
        picker.WaitForAssertion(() => Assert.Equal(2, picker.FindAll(".item-region").Count));
        picker.FindAll(".item-region")[1].Click();
        picker
            .FindAll("button")
            .Single(b => b.TextContent.Trim() == "Review selected items")
            .Click();
        picker.WaitForAssertion(() => Assert.NotNull(reviewed));
        Assert.Equal("Lamp", Assert.Single(reviewed!).Detection.Label);
        Assert.Single(_storage.References);
        var crop = Assert.Single(_module.Invocations, i => i.Identifier == "cropStream");
        Assert.Equal(new BoundingBox(0.6, 0.1, 0.2, 0.4), crop.Arguments[2]);
    }

    [Fact]
    public void SelectHighConfidence_SelectsItemsAboveThreshold_AndDeselectAllClears()
    {
        _detector.Detect = (_, _) =>
            Task.FromResult<IReadOnlyList<DetectedAsset>>([
                new("Chair", 0.9, new(0.1, 0.1, 0.3, 0.3), null, []),
                new("Pillow", 0.5, new(0.6, 0.1, 0.2, 0.4), null, []),
            ]);

        var picker = _context.Render<VideoScanPicker>();
        picker.Find("#room-video").Change("room.mp4");
        picker.Find("button").Click();
        picker.WaitForAssertion(() => Assert.Equal(2, picker.FindAll(".item-region").Count));

        picker
            .FindAll("button")
            .Single(b => b.TextContent.Trim() == "Select high-confidence items")
            .Click();
        Assert.Contains(picker.FindAll(".item-region")[0].ClassList, c => c == "selected");
        Assert.DoesNotContain(picker.FindAll(".item-region")[1].ClassList, c => c == "selected");

        picker.FindAll("button").Single(b => b.TextContent.Trim() == "Deselect all").Click();
        Assert.DoesNotContain(picker.FindAll(".item-region")[0].ClassList, c => c == "selected");
        Assert.DoesNotContain(picker.FindAll(".item-region")[1].ClassList, c => c == "selected");
    }

    [Fact]
    public void SegmentedItem_RendersPolygonOverlay_AndPassesMaskToCropStream()
    {
        IReadOnlyList<NormalizedPoint> mask =
        [
            new(0.1, 0.1),
            new(0.4, 0.1),
            new(0.4, 0.4),
            new(0.1, 0.4),
        ];
        _detector.Detect = (_, _) =>
            Task.FromResult<IReadOnlyList<DetectedAsset>>([
                new("Chair", 0.9, new(0.1, 0.1, 0.3, 0.3), null, [], mask),
            ]);

        var picker = _context.Render<VideoScanPicker>();
        picker.Find("#room-video").Change("room.mp4");
        picker.Find("button").Click();
        picker.WaitForAssertion(() => Assert.Single(picker.FindAll("polygon.item-polygon")));

        var polygon = picker.Find("polygon.item-polygon");
        Assert.Contains("10.0,10.0 40.0,10.0", polygon.GetAttribute("points"));
        Assert.Contains("Segmented", picker.Markup);

        polygon.Click();
        Assert.Contains("selected", polygon.ClassList);

        picker
            .FindAll("button")
            .Single(b => b.TextContent.Trim() == "Review selected items")
            .Click();
        picker.WaitForAssertion(() => Assert.Single(_storage.References));

        var crop = Assert.Single(_module.Invocations, i => i.Identifier == "cropStream");
        Assert.NotNull(crop.Arguments[3]);
    }

    [Fact]
    public void MissingBoxes_UseCheckbox_AndFailedFramesDoNotDiscardSuccessfulOnes()
    {
        _detector.Detect = (call, _) =>
            call == 1
                ? Task.FromException<IReadOnlyList<DetectedAsset>>(
                    new AssetDetectionException("Provider unavailable")
                )
                : Task.FromResult<IReadOnlyList<DetectedAsset>>([
                    new("Chair", 0.9, null, null, []),
                ]);
        var picker = _context.Render<VideoScanPicker>();
        picker.Find("#room-video").Change("room.mp4");
        picker.Find("button").Click();
        picker.WaitForAssertion(() =>
            Assert.Contains("1 frame(s) could not be analyzed", picker.Markup)
        );
        picker.FindAll("button").Single(b => b.TextContent.Trim() == "1.5 s").Click();
        Assert.Empty(picker.FindAll(".item-region"));
        Assert.Contains("saves the whole frame", picker.Markup);
        picker.Find("input[type=checkbox]").Change(true);
        picker
            .FindAll("button")
            .Single(b => b.TextContent.Trim() == "Review selected items")
            .Click();
        picker.WaitForAssertion(() => Assert.Single(_storage.References));
        Assert.Null(
            Assert.Single(_module.Invocations, i => i.Identifier == "cropStream").Arguments[2]
        );
    }

    [Fact]
    public async Task CancelDetection_ReleasesBusyState_WithoutSavingImages()
    {
        _detector.Detect = async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return [];
        };
        var picker = _context.Render<VideoScanPicker>();
        picker.Find("#room-video").Change("room.mp4");
        var scan = picker.Find("button").ClickAsync(new MouseEventArgs());
        picker.WaitForAssertion(() =>
            Assert.True(picker.Find("#room-video").HasAttribute("disabled"))
        );
        picker.FindAll("button").Single(b => b.TextContent.Trim() == "Cancel").Click();
        await scan;
        picker.WaitForAssertion(() => Assert.Contains("Scan cancelled", picker.Markup));
        Assert.False(picker.Find("#room-video").HasAttribute("disabled"));
        Assert.Empty(_storage.References);
    }

    [Fact]
    public void SaveFailure_PreservesSelection_AndRetryReusesAlreadySavedCrop()
    {
        _storage.FailOnCall = 2;
        var picker = _context.Render<VideoScanPicker>();
        picker.Find("#room-video").Change("room.mp4");
        picker.Find("button").Click();
        picker.WaitForAssertion(() => Assert.Equal(2, picker.FindAll(".item-region").Count));
        picker.FindAll(".item-region")[0].Click();
        picker.FindAll(".item-region")[1].Click();
        picker
            .FindAll("button")
            .Single(b => b.TextContent.Trim() == "Review selected items")
            .Click();
        picker.WaitForAssertion(() => Assert.Contains("selection is preserved", picker.Markup));
        Assert.Single(_storage.References);
        picker
            .FindAll("button")
            .Single(b => b.TextContent.Trim() == "Review selected items")
            .Click();
        picker.WaitForAssertion(() =>
            Assert.Contains("Selected items are ready below", picker.Markup)
        );
        Assert.Equal(2, _storage.References.Count);
        Assert.Equal(3, _storage.Calls);
    }

    [Fact]
    public async Task CancelSaving_PreservesSelection_ForRetry()
    {
        _storage.WaitForCancellation = true;
        var picker = _context.Render<VideoScanPicker>();
        picker.Find("#room-video").Change("room.mp4");
        picker.Find("button").Click();
        picker.WaitForAssertion(() => Assert.Equal(2, picker.FindAll(".item-region").Count));
        picker.FindAll(".item-region")[0].Click();
        var save = picker
            .FindAll("button")
            .Single(b => b.TextContent.Trim() == "Review selected items")
            .ClickAsync(new MouseEventArgs());
        picker.WaitForAssertion(() =>
            Assert.Contains("Saving selected item images", picker.Markup)
        );
        picker.FindAll("button").Single(b => b.TextContent.Trim() == "Cancel").Click();
        await save;
        picker.WaitForAssertion(() => Assert.Contains("Saving cancelled", picker.Markup));
        Assert.Empty(_storage.References);
        Assert.Equal("true", picker.FindAll(".item-region")[0].GetAttribute("aria-pressed"));
        _storage.WaitForCancellation = false;
        picker
            .FindAll("button")
            .Single(b => b.TextContent.Trim() == "Review selected items")
            .Click();
        picker.WaitForAssertion(() => Assert.Single(_storage.References));
    }

    [Fact]
    public void DecodeFailure_ShowsActionableError_AndAllowsRetry()
    {
        _module
            .Setup<double[]>("prepare", _ => true)
            .SetException(new JSException("Choose a video up to 30 seconds long.\nstack trace"));
        var picker = _context.Render<VideoScanPicker>();
        picker.Find("#room-video").Change("room.mp4");
        picker.Find("button").Click();
        picker.WaitForAssertion(() =>
            Assert.Contains(
                "Choose a video up to 30 seconds long.",
                picker.Find("[role=alert]").TextContent
            )
        );
        Assert.DoesNotContain("stack trace", picker.Markup);
        Assert.False(picker.Find("#room-video").HasAttribute("disabled"));
        Assert.Empty(_storage.References);
    }

    [Fact]
    public async Task NoMarketEvidence_ShowsUnavailable_WithoutSaveOrMergeActions()
    {
        await _repository.AddAsync(new PhysicalAssetInput("Chair", null, null, 50));
        _valuation.Result = MarketValuationCalculator.Calculate([], DateTimeOffset.UtcNow);
        var page = _context.Render<ScanPage>(p => p.Add(x => x.ShowVideoScan, true));
        page.Find("#room-video").Change("room.mp4");
        await page.FindAll("button")
            .Single(b => b.TextContent.Trim() == "Find items in video")
            .ClickAsync(new MouseEventArgs());
        page.FindAll(".item-region")[0].Click();
        await page.FindAll("button")
            .Single(b => b.TextContent.Trim() == "Review selected items")
            .ClickAsync(new MouseEventArgs());

        Assert.Contains("No market value available", page.Markup);
        Assert.Contains("Market evidence", page.Markup);
        Assert.DoesNotContain("Save as new item", page.Markup);
        Assert.DoesNotContain("Merge into", page.Markup);
        Assert.Single(await _repository.GetAllAsync());
        Assert.Single(_db.AssetValuationRecords);
    }

    [Fact]
    public async Task SparseMarketEvidence_SavesWithoutConfidenceCaveat()
    {
        _valuation.Result = MarketValuationCalculator.Calculate(
            [new(25, "Market", "Chair", "Used")],
            DateTimeOffset.UtcNow
        );
        var page = _context.Render<ScanPage>(p => p.Add(x => x.ShowVideoScan, true));
        page.Find("#room-video").Change("room.mp4");
        await page.FindAll("button")
            .Single(b => b.TextContent.Trim() == "Find items in video")
            .ClickAsync(new MouseEventArgs());
        page.FindAll(".item-region")[0].Click();
        await page.FindAll("button")
            .Single(b => b.TextContent.Trim() == "Review selected items")
            .ClickAsync(new MouseEventArgs());
        Assert.Contains("Market evidence", page.Markup);
        Assert.DoesNotContain("low confidence", page.Markup, StringComparison.OrdinalIgnoreCase);
        await page.FindAll("button")
            .Single(b => b.TextContent.Trim() == "Save as new item")
            .ClickAsync(new MouseEventArgs());

        var record = Assert.Single(_db.AssetValuationRecords);
        Assert.Equal(25m, record.EstimatedValue);
        Assert.Equal("Market evidence", record.Source);
        Assert.Contains("comparable listing", record.Notes);
    }

    [Fact]
    public async Task NoMarketEvidence_OnRevalue_PreservesPreviousValuation()
    {
        var id = await _repository.AddAsync(new PhysicalAssetInput("Chair", null, null, 50));
        _valuation.Result = MarketValuationCalculator.Calculate([], DateTimeOffset.UtcNow);
        var page = _context.Render<InventoryList>();
        await page.Find(".asset-row-summary").ClickAsync(new MouseEventArgs());
        await page.FindAll("button")
            .Single(b => b.TextContent.Trim() == "Revalue")
            .ClickAsync(new MouseEventArgs());

        Assert.Contains("No usable comparable listings", page.Find("[role=alert]").TextContent);
        var detail = await _repository.GetByIdAsync(id);
        Assert.Equal(50m, Assert.Single(detail!.ValuationHistory).EstimatedValue);
    }

    public void Dispose()
    {
        _context.Dispose();
        _db.Dispose();
        _connection.Dispose();
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

    private sealed class FakeDetector : IPhysicalAssetDetectionService
    {
        private int _calls;
        public Func<
            int,
            CancellationToken,
            Task<IReadOnlyList<DetectedAsset>>
        > Detect
        { get; set; } =
            (_, _) =>
                Task.FromResult<IReadOnlyList<DetectedAsset>>([
                    new("Chair", 0.9, new(0.1, 0.1, 0.3, 0.3), null, []),
                    new("Lamp", 0.8, new(0.6, 0.1, 0.2, 0.4), null, []),
                ]);

        public Task<IReadOnlyList<DetectedAsset>> DetectAsync(
            byte[] imageBytes,
            string mediaType,
            CancellationToken cancellationToken = default
        ) => Detect(++_calls, cancellationToken);

        public Task<DetectedAsset> IdentifyCutoutAsync(
            byte[] imageBytes,
            string mediaType,
            CancellationToken cancellationToken = default
        ) =>
            Task.FromResult(
                new DetectedAsset(
                    "Chair",
                    0.95,
                    new BoundingBox(0.1, 0.1, 0.3, 0.3),
                    new AssetIdentification("Brand", "Model", 0.95),
                    []
                )
            );
    }

    private sealed class FakeStorage : IPossessionImageStorage
    {
        public List<string> References { get; } = [];
        public int Calls { get; private set; }
        public int FailOnCall { get; set; }
        public bool WaitForCancellation { get; set; }

        public async Task<string> SaveAsync(
            Stream content,
            string fileName,
            CancellationToken cancellationToken = default
        )
        {
            if (WaitForCancellation)
                await Task.Delay(Timeout.Infinite, cancellationToken);
            if (++Calls == FailOnCall)
                throw new PossessionImageStorageException("Test disk failure");
            var reference = $"item-{Calls}.jpg";
            References.Add(reference);
            return reference;
        }

        public Task<Stream?> OpenReadAsync(
            string reference,
            CancellationToken cancellationToken = default
        ) => Task.FromResult<Stream?>(null);
    }

    private sealed class FakeValuation : IAssetValuationService
    {
        public AssetValuation Result { get; set; } =
            new(100, "Test estimate", DateTimeOffset.UtcNow, true);

        public Task<AssetValuation> EstimateAsync(
            string label,
            string? brand,
            string? model,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(Result);
    }
}
