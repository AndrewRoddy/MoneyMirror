using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MoneyMirror.Data;
using MoneyMirror.PhysicalAssets;

namespace MoneyMirror.Tests.PhysicalAssets;

/// <summary>
/// Integration tests: exercises real EF Core behavior against a real
/// SQLite database, since no PostgreSQL server is available in this
/// environment. No mocking of EF Core itself.
/// </summary>
public class EfPhysicalAssetRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly MoneyMirrorDbContext _db;
    private readonly EfPhysicalAssetRepository _repository;

    public EfPhysicalAssetRepositoryTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<MoneyMirrorDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new MoneyMirrorDbContext(options);
        _db.Database.EnsureCreated();

        _repository = new EfPhysicalAssetRepository(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task AddAsync_WithInitialValuation_IsReturnedByGetAllWithThatValuation()
    {
        await _repository.AddAsync(
            new PhysicalAssetInput("Guitar", "Music", "Fender CD-60S", 120m)
        );

        var all = await _repository.GetAllAsync();

        var asset = Assert.Single(all);
        Assert.Equal("Guitar", asset.Name);
        Assert.Equal("Fender CD-60S", asset.IdentifiedProductModel);
        Assert.Equal(120m, asset.CurrentValuation);
        Assert.True(asset.IsManuallyAdded);
    }

    [Fact]
    public async Task AddAsync_WithoutValuation_HasNullCurrentValuation()
    {
        await _repository.AddAsync(new PhysicalAssetInput("Lamp", null, null, null));

        var asset = Assert.Single(await _repository.GetAllAsync());

        Assert.Null(asset.CurrentValuation);
        Assert.Null(asset.ValuationDate);
    }

    [Fact]
    public async Task UpdateAsync_ExistingItem_ChangesItsFields()
    {
        var id = await _repository.AddAsync(
            new PhysicalAssetInput("Old Name", "Old Category", null, null)
        );

        var updated = await _repository.UpdateAsync(
            id,
            new PhysicalAssetInput("New Name", "New Category", "Model X", null)
        );

        Assert.True(updated);
        var asset = Assert.Single(await _repository.GetAllAsync());
        Assert.Equal("New Name", asset.Name);
        Assert.Equal("New Category", asset.Category);
        Assert.Equal("Model X", asset.IdentifiedProductModel);
    }

    [Fact]
    public async Task UpdateAsync_WithNewValuation_UpdatesCurrentValuation()
    {
        var id = await _repository.AddAsync(new PhysicalAssetInput("Guitar", null, null, 100m));

        await _repository.UpdateAsync(id, new PhysicalAssetInput("Guitar", null, null, 150m));

        var asset = Assert.Single(await _repository.GetAllAsync());
        Assert.Equal(150m, asset.CurrentValuation);
    }

    [Fact]
    public async Task UpdateAsync_UnknownId_ReturnsFalse()
    {
        var updated = await _repository.UpdateAsync(
            Guid.NewGuid(),
            new PhysicalAssetInput("X", null, null, null)
        );

        Assert.False(updated);
    }

    [Fact]
    public async Task DeleteAsync_ExistingItem_RemovesItAndItsValuationRecords()
    {
        var id = await _repository.AddAsync(new PhysicalAssetInput("Guitar", null, null, 120m));

        var deleted = await _repository.DeleteAsync(id);

        Assert.True(deleted);
        Assert.Empty(await _repository.GetAllAsync());
        Assert.Empty(_db.AssetValuationRecords);
    }

    [Fact]
    public async Task DeleteAsync_UnknownId_ReturnsFalse()
    {
        var deleted = await _repository.DeleteAsync(Guid.NewGuid());

        Assert.False(deleted);
    }

    [Fact]
    public async Task GetAllAsync_MultipleItems_SumsToCorrectTotalValue()
    {
        await _repository.AddAsync(new PhysicalAssetInput("A", null, null, 100m));
        await _repository.AddAsync(new PhysicalAssetInput("B", null, null, 50m));
        await _repository.AddAsync(new PhysicalAssetInput("C", null, null, null));

        var all = await _repository.GetAllAsync();

        Assert.Equal(3, all.Count);
        Assert.Equal(150m, all.Sum(a => a.CurrentValuation ?? 0));
    }

    [Fact]
    public async Task AddFromScanAsync_CreatesScannedItemWithValuationAndEvidence()
    {
        var id = await _repository.AddFromScanAsync(
            new ScannedAssetInput(
                "Guitar",
                "Music",
                "Fender CD-60S",
                "abc123.jpg",
                120m,
                "Typical used price."
            )
        );

        var summary = Assert.Single(await _repository.GetAllAsync());
        Assert.Equal(id, summary.Id);
        Assert.False(summary.IsManuallyAdded);
        Assert.Equal("abc123.jpg", summary.ImageReference);
        Assert.Equal(120m, summary.CurrentValuation);

        var detail = await _repository.GetByIdAsync(id);
        Assert.NotNull(detail);
        var entry = Assert.Single(detail!.ValuationHistory);
        Assert.Equal(120m, entry.EstimatedValue);
        Assert.Equal("Typical used price.", entry.Notes);
        Assert.Equal(string.Empty, entry.Source);
    }

    [Fact]
    public async Task GetByIdAsync_UnknownId_ReturnsNull()
    {
        var detail = await _repository.GetByIdAsync(Guid.NewGuid());

        Assert.Null(detail);
    }

    [Fact]
    public async Task GetByIdAsync_MultipleValuations_ReturnsFullHistoryNewestFirst()
    {
        var id = await _repository.AddAsync(new PhysicalAssetInput("Guitar", null, null, 100m));
        await _repository.UpdateAsync(id, new PhysicalAssetInput("Guitar", null, null, 150m));

        var detail = await _repository.GetByIdAsync(id);

        Assert.NotNull(detail);
        Assert.Equal(2, detail!.ValuationHistory.Count);
        Assert.Equal(150m, detail.ValuationHistory[0].EstimatedValue);
        Assert.Equal(100m, detail.ValuationHistory[1].EstimatedValue);
    }

    [Fact]
    public async Task AddValuationAsync_ExistingItem_AppendsToHistoryWithoutRemovingPriorEntries()
    {
        var id = await _repository.AddAsync(new PhysicalAssetInput("Guitar", null, null, 100m));

        var updated = await _repository.AddValuationAsync(
            id,
            175m,
            "AI estimate (revalue)",
            "Prices went up."
        );

        Assert.True(updated);
        var detail = await _repository.GetByIdAsync(id);
        Assert.NotNull(detail);
        Assert.Equal(2, detail!.ValuationHistory.Count);
        Assert.Equal(175m, detail.ValuationHistory[0].EstimatedValue);
        Assert.Equal("Prices went up.", detail.ValuationHistory[0].Notes);
        Assert.Equal(100m, detail.ValuationHistory[1].EstimatedValue);

        var summary = Assert.Single(await _repository.GetAllAsync());
        Assert.Equal(175m, summary.CurrentValuation);
    }

    [Fact]
    public async Task AddValuationAsync_UnknownId_ReturnsFalse()
    {
        var updated = await _repository.AddValuationAsync(
            Guid.NewGuid(),
            100m,
            "AI estimate",
            null
        );

        Assert.False(updated);
    }

    // #234: comparable listings must round-trip through save/reload, and a
    // later revalue's evidence must never bleed into an earlier entry's.
    [Fact]
    public async Task AddFromScanAsync_WithComparableListings_PersistsAndReloadsEvidence()
    {
        IReadOnlyList<AssetValuationEvidence> comps =
        [
            new(115m, "Reverb", "Fender CD-60S, used", "Good"),
            new(130m, "Marketplace", "Fender CD-60S dreadnought", null),
        ];

        var id = await _repository.AddFromScanAsync(
            new ScannedAssetInput(
                "Guitar",
                "Music",
                "Fender CD-60S",
                "abc123.jpg",
                122.5m,
                "Median of 2 listings.",
                "Market evidence",
                comps
            )
        );

        var detail = await _repository.GetByIdAsync(id);

        Assert.NotNull(detail);
        var entry = Assert.Single(detail!.ValuationHistory);
        Assert.Equal(2, entry.ComparableListings.Count);
        Assert.Equal(115m, entry.ComparableListings[0].PriceUsd);
        Assert.Equal("Reverb", entry.ComparableListings[0].Source);
        Assert.Equal("Fender CD-60S, used", entry.ComparableListings[0].ListingTitle);
        Assert.Equal("Good", entry.ComparableListings[0].Condition);
        Assert.Null(entry.ComparableListings[1].Condition);
    }

    [Fact]
    public async Task AddValuationAsync_RevalueWithNewEvidence_DoesNotAffectPriorEntrysEvidence()
    {
        IReadOnlyList<AssetValuationEvidence> firstComps = [new(100m, "Reverb", null, null)];
        IReadOnlyList<AssetValuationEvidence> secondComps =
        [
            new(140m, "Marketplace", null, null),
            new(160m, "Craigslist", null, null),
        ];

        var id = await _repository.AddFromScanAsync(
            new ScannedAssetInput(
                "Guitar",
                null,
                null,
                null,
                100m,
                "First estimate.",
                "Market evidence",
                firstComps
            )
        );
        await _repository.AddValuationAsync(id, 150m, "Market evidence", "Revalued.", secondComps);

        var detail = await _repository.GetByIdAsync(id);

        Assert.NotNull(detail);
        Assert.Equal(2, detail!.ValuationHistory.Count);
        Assert.Equal(2, detail.ValuationHistory[0].ComparableListings.Count); // Newest first: the revalue.
        Assert.Equal(150m, detail.ValuationHistory[0].EstimatedValue);
        Assert.Single(detail.ValuationHistory[1].ComparableListings); // The original scan's evidence is untouched.
        Assert.Equal(100m, detail.ValuationHistory[1].EstimatedValue);
    }

    [Fact]
    public async Task AddValuationAsync_WithoutComparableListings_LeavesEvidenceEmptyNotNull()
    {
        var id = await _repository.AddAsync(new PhysicalAssetInput("Guitar", null, null, null));

        await _repository.AddValuationAsync(id, 100m, "AI estimate", "No comps found.");

        var detail = await _repository.GetByIdAsync(id);
        Assert.NotNull(detail);
        var entry = Assert.Single(detail!.ValuationHistory);
        Assert.Empty(entry.ComparableListings);
        Assert.Equal(100m, entry.EstimatedValue); // No-evidence never becomes a zero-dollar valuation.
    }

    [Fact]
    public async Task FindPossibleDuplicatesAsync_MatchingProductModel_ReturnsExistingItem()
    {
        await _repository.AddAsync(
            new PhysicalAssetInput("Guitar", "Music", "Fender CD-60S", 120m)
        );

        var duplicates = await _repository.FindPossibleDuplicatesAsync(
            "Acoustic guitar",
            "fender cd-60s"
        );

        var duplicate = Assert.Single(duplicates);
        Assert.Equal("Guitar", duplicate.Name);
    }

    [Fact]
    public async Task FindPossibleDuplicatesAsync_MatchingNameOnly_ReturnsExistingItem()
    {
        await _repository.AddAsync(new PhysicalAssetInput("Lamp", null, null, null));

        var duplicates = await _repository.FindPossibleDuplicatesAsync("lamp", null);

        Assert.Single(duplicates);
    }

    [Fact]
    public async Task FindPossibleDuplicatesAsync_NoMatch_ReturnsEmpty()
    {
        await _repository.AddAsync(
            new PhysicalAssetInput("Guitar", "Music", "Fender CD-60S", 120m)
        );

        var duplicates = await _repository.FindPossibleDuplicatesAsync("Lamp", "IKEA Foto");

        Assert.Empty(duplicates);
    }

    [Fact]
    public async Task FindPossibleDuplicatesAsync_DifferentProductModelButSameName_StillMatchesOnName()
    {
        await _repository.AddAsync(
            new PhysicalAssetInput("Guitar", "Music", "Fender CD-60S", 120m)
        );

        var duplicates = await _repository.FindPossibleDuplicatesAsync("Guitar", "Gibson Les Paul");

        var duplicate = Assert.Single(duplicates);
        Assert.Equal("Guitar", duplicate.Name);
    }
}
