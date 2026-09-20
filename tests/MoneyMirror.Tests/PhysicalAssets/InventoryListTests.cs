using Bunit;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MoneyMirror.Data;
using MoneyMirror.Features.PhysicalAssets;
using MoneyMirror.PhysicalAssets;

namespace MoneyMirror.Tests.PhysicalAssets;

/// <summary>Real rendered component and SQLite repository; only the AI valuation call is faked.</summary>
public sealed class InventoryListTests : IDisposable
{
    private readonly BunitContext _context = new();
    private readonly SqliteConnection _connection = new("Filename=:memory:");
    private readonly MoneyMirrorDbContext _db;
    private readonly EfPhysicalAssetRepository _repository;
    private readonly FakeValuation _valuation = new();

    public InventoryListTests()
    {
        _connection.Open();
        _db = new MoneyMirrorDbContext(new DbContextOptionsBuilder<MoneyMirrorDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        _repository = new EfPhysicalAssetRepository(_db);
        _context.Services.AddSingleton<IPhysicalAssetRepository>(_repository);
        _context.Services.AddSingleton<IAssetValuationService>(_valuation);
    }

    // #237: "Add Item" should value the item automatically, the same way
    // scanning a possession does - never asking the user to type a price.
    [Fact]
    public async Task AddItem_EstimatesValueAutomatically_JustLikeScanning()
    {
        var page = _context.Render<InventoryList>();

        Assert.DoesNotContain(page.FindAll("input"), i => i.GetAttribute("placeholder") == "Value (optional)");

        page.Find("input[placeholder='Name']").Change("Standing Desk");
        page.Find("input[placeholder='Category']").Change("Furniture");
        await page.Find("button.btn-primary").ClickAsync(new());

        page.WaitForAssertion(() => Assert.Single(page.FindAll(".asset-row")));

        var saved = Assert.Single(await _repository.GetAllAsync());
        Assert.Equal("Standing Desk", saved.Name);
        Assert.Equal(_valuation.EstimatedValue, saved.CurrentValuation);
        Assert.True(saved.IsManuallyAdded);

        var detail = await _repository.GetByIdAsync(saved.Id);
        var entry = Assert.Single(detail!.ValuationHistory);
        Assert.Contains("AI estimate", entry.Source);
        Assert.Equal("Standing Desk", _valuation.LastLabel);
    }

    [Fact]
    public async Task AddItem_WhenEstimationFails_StillCreatesTheItem()
    {
        _valuation.ShouldFail = true;
        var page = _context.Render<InventoryList>();

        page.Find("input[placeholder='Name']").Change("Mystery Item");
        await page.Find("button.btn-primary").ClickAsync(new());

        page.WaitForAssertion(() => Assert.Contains("could not be estimated", page.Markup));

        var saved = Assert.Single(await _repository.GetAllAsync());
        Assert.Equal("Mystery Item", saved.Name);
        Assert.Null(saved.CurrentValuation);
    }

    // #71: insufficient evidence must never become a fabricated/zero-dollar
    // valuation - the item is still created, just left unvalued.
    [Fact]
    public async Task AddItem_WhenEstimateHasNoValue_StillCreatesTheItemUnvalued()
    {
        _valuation.HasNoValue = true;
        var page = _context.Render<InventoryList>();

        page.Find("input[placeholder='Name']").Change("Rare Item");
        await page.Find("button.btn-primary").ClickAsync(new());

        page.WaitForAssertion(() => Assert.Contains("could not be estimated", page.Markup));

        var saved = Assert.Single(await _repository.GetAllAsync());
        Assert.Equal("Rare Item", saved.Name);
        Assert.Null(saved.CurrentValuation);

        var detail = await _repository.GetByIdAsync(saved.Id);
        Assert.Empty(detail!.ValuationHistory);
    }

    public void Dispose()
    {
        _context.Dispose();
        _db.Dispose();
        _connection.Dispose();
    }

    private sealed class FakeValuation : IAssetValuationService
    {
        public decimal EstimatedValue { get; } = 250m;
        public bool ShouldFail { get; set; }
        public bool HasNoValue { get; set; }
        public string? LastLabel { get; private set; }

        public Task<AssetValuation> EstimateAsync(string label, string? brand, string? model, CancellationToken cancellationToken = default)
        {
            LastLabel = label;
            if (ShouldFail)
            {
                throw new AssetValuationException("The LLM provider call failed.");
            }

            var value = HasNoValue ? (decimal?)null : EstimatedValue;
            return Task.FromResult(new AssetValuation(value, "Test estimate", DateTimeOffset.UtcNow, true));
        }
    }
}
