namespace MoneyMirror.PhysicalAssets;

/// <summary>Builds an asset valuation from structured market listings, without an LLM.</summary>
public sealed class EvidenceBasedAssetValuationService : IAssetValuationService
{
    private readonly ISerpApiMarketDataService _marketDataService;
    private readonly TimeProvider _timeProvider;

    public EvidenceBasedAssetValuationService(
        ISerpApiMarketDataService marketDataService,
        TimeProvider? timeProvider = null
    )
    {
        _marketDataService = marketDataService;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<AssetValuation> EstimateAsync(
        string label,
        string? brand,
        string? model,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var evidence = await _marketDataService.SearchUsedListingsAsync(
                label,
                brand,
                model,
                cancellationToken
            );
            return MarketValuationCalculator.Calculate(evidence, _timeProvider.GetUtcNow());
        }
        catch (SerpApiMarketDataException ex)
        {
            throw new AssetValuationException("Failed to retrieve comparable market listings.", ex);
        }
    }
}
