namespace MoneyMirror.PhysicalAssets;

/// <summary>Searches an external market-data provider for priced used-item comparables.</summary>
public interface IMarketDataService
{
    /// <summary>Returns priced used/refurbished listings matching an item's label, brand and model.</summary>
    Task<IReadOnlyList<AssetValuationEvidence>> SearchUsedListingsAsync(
        string label,
        string? brand,
        string? model,
        CancellationToken cancellationToken = default
    );
}
