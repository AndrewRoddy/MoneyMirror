namespace MoneyMirror.Data.Entities;

public class AssetValuationRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PhysicalAssetId { get; set; }
    public decimal EstimatedValue { get; set; }
    public DateTimeOffset ValuedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? Source { get; set; }
    public string? Notes { get; set; }

    public PhysicalAsset PhysicalAsset { get; set; } = null!;

    /// <summary>The comparable listings this valuation was derived from, if any (#234).</summary>
    public ICollection<AssetValuationEvidenceRecord> Evidence { get; set; } =
        new List<AssetValuationEvidenceRecord>();
}

/// <summary>One comparable market listing that supported an <see cref="AssetValuationRecord"/>.</summary>
public class AssetValuationEvidenceRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AssetValuationRecordId { get; set; }
    public decimal PriceUsd { get; set; }
    public string Source { get; set; } = string.Empty;
    public string? ListingTitle { get; set; }
    public string? Condition { get; set; }

    public AssetValuationRecord AssetValuationRecord { get; set; } = null!;
}
