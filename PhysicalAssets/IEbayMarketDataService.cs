namespace MoneyMirror.PhysicalAssets;

/// <summary>Searches a market data source for real listings comparable to an identified item.</summary>
public interface IEbayMarketDataService
{
    /// <summary>Finds up to twenty used USD listings matching the confirmed item's label, brand and model.</summary>
    Task<IReadOnlyList<AssetValuationEvidence>> SearchUsedListingsAsync(
        string label,
        string? brand,
        string? model,
        CancellationToken cancellationToken = default
    );
}
