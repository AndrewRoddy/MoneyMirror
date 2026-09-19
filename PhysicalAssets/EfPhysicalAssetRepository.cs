using Microsoft.EntityFrameworkCore;
using MoneyMirror.Data;
using DataEntities = MoneyMirror.Data.Entities;

namespace MoneyMirror.PhysicalAssets;

/// <summary>
/// <see cref="IPhysicalAssetRepository"/> implementation backed by
/// <see cref="MoneyMirrorDbContext"/>.
/// </summary>
public class EfPhysicalAssetRepository : IPhysicalAssetRepository
{
    private readonly MoneyMirrorDbContext _db;

    public EfPhysicalAssetRepository(MoneyMirrorDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<PhysicalAssetSummary>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var assets = await _db.PhysicalAssets.Include(a => a.ValuationRecords).ToListAsync(cancellationToken);

        return assets.Select(ToSummary).OrderBy(a => a.Name).ToList();
    }

    public async Task<Guid> AddAsync(PhysicalAssetInput input, CancellationToken cancellationToken = default)
    {
        var asset = new DataEntities.PhysicalAsset
        {
            Name = input.Name,
            Category = input.Category,
            IdentifiedProductModel = input.IdentifiedProductModel,
            Source = DataEntities.PhysicalAssetSource.Manual,
        };
        _db.PhysicalAssets.Add(asset);

        if (input.InitialValuation is { } valuation)
        {
            var record = new DataEntities.AssetValuationRecord
            {
                PhysicalAssetId = asset.Id,
                EstimatedValue = valuation,
                Source = "Manual entry",
            };
            _db.AssetValuationRecords.Add(record);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return asset.Id;
    }

    public async Task<bool> UpdateAsync(
        Guid id,
        PhysicalAssetInput input,
        CancellationToken cancellationToken = default)
    {
        var asset = await _db.PhysicalAssets.FindAsync([id], cancellationToken);
        if (asset is null)
        {
            return false;
        }

        asset.Name = input.Name;
        asset.Category = input.Category;
        asset.IdentifiedProductModel = input.IdentifiedProductModel;
        asset.UpdatedAt = DateTimeOffset.UtcNow;

        if (input.InitialValuation is { } valuation)
        {
            _db.AssetValuationRecords.Add(
                new DataEntities.AssetValuationRecord
                {
                    PhysicalAssetId = asset.Id,
                    EstimatedValue = valuation,
                    Source = "Manual entry",
                }
            );
        }

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var asset = await _db.PhysicalAssets.FindAsync([id], cancellationToken);
        if (asset is null)
        {
            return false;
        }

        _db.PhysicalAssets.Remove(asset);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static PhysicalAssetSummary ToSummary(DataEntities.PhysicalAsset asset)
    {
        var latest = asset.ValuationRecords.OrderByDescending(r => r.ValuedAt).FirstOrDefault();

        return new PhysicalAssetSummary(
            asset.Id,
            asset.Name,
            asset.Category,
            asset.IdentifiedProductModel,
            asset.ImageReference,
            asset.Source == DataEntities.PhysicalAssetSource.Manual,
            latest?.EstimatedValue,
            latest?.ValuedAt);
    }
}
