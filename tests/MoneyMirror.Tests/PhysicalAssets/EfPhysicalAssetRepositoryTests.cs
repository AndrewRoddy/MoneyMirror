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

        var options = new DbContextOptionsBuilder<MoneyMirrorDbContext>().UseSqlite(_connection).Options;
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
        await _repository.AddAsync(new PhysicalAssetInput("Guitar", "Music", "Fender CD-60S", 120m));

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
        var id = await _repository.AddAsync(new PhysicalAssetInput("Old Name", "Old Category", null, null));

        var updated = await _repository.UpdateAsync(id, new PhysicalAssetInput("New Name", "New Category", "Model X", null));

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
        var updated = await _repository.UpdateAsync(Guid.NewGuid(), new PhysicalAssetInput("X", null, null, null));

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
}
