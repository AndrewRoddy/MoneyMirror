namespace MoneyMirror.PhysicalAssets;

/// <summary>
/// A comparable market listing that supports an asset valuation.
/// </summary>
public record AssetValuationEvidence(
    decimal PriceUsd,
    string Source,
    string? ListingTitle,
    string? Condition);
