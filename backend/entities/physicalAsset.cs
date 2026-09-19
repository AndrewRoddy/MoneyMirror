namespace MoneyMirror.Data.Entities;

public enum PhysicalAssetSource
{
    Manual,
    Scanned,
}

public class PhysicalAsset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? Description { get; set; }

    /// <summary>The identified product/model, e.g. from PA3's detection - null when not identified.</summary>
    public string? IdentifiedProductModel { get; set; }

    /// <summary>Reference into IPossessionImageStorage for the item's photo, if any.</summary>
    public string? ImageReference { get; set; }

    public PhysicalAssetSource Source { get; set; } = PhysicalAssetSource.Manual;

    public decimal? PurchasePrice { get; set; }
    public DateTimeOffset? AcquiredAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<AssetValuationRecord> ValuationRecords { get; set; } =
        new List<AssetValuationRecord>();
}
