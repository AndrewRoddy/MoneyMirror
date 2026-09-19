namespace MoneyMirror.PhysicalAssets;

/// <summary>
/// A physical asset for display: its own fields plus the current
/// valuation, derived as the most recent valuation record (null when the
/// item has never been valued).
/// </summary>
public record PhysicalAssetSummary(
    Guid Id,
    string Name,
    string? Category,
    string? IdentifiedProductModel,
    string? ImageReference,
    bool IsManuallyAdded,
    decimal? CurrentValuation,
    DateTimeOffset? ValuationDate);
