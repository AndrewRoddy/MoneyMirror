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
}
