namespace MoneyMirror.PhysicalAssets;

/// <summary>Searches Google results through SerpApi for priced used-item comparables.</summary>
public interface ISerpApiMarketDataService
{
    /// <summary>Returns up to twenty used USD listings matching an item's label, brand and model.</summary>
    Task<IReadOnlyList<AssetValuationEvidence>> SearchUsedListingsAsync(
        string label,
        string? brand,
        string? model,
        CancellationToken cancellationToken = default
    );
}
