namespace PittMoney.Data.Entities;

public class PhysicalAsset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? Description { get; set; }
    public decimal? PurchasePrice { get; set; }
    public DateTimeOffset? AcquiredAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<AssetValuationRecord> ValuationRecords { get; set; } =
        new List<AssetValuationRecord>();
}
