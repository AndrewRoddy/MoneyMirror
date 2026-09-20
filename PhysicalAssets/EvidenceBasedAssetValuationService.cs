namespace MoneyMirror.PhysicalAssets;

/// <summary>Builds an asset valuation from structured market listings, without an LLM.
/// Falls back to <see cref="AiEstimatedValuationService"/>'s price guess when the market-data
/// provider itself fails (credentials, network, rate limit) - not when it succeeds with zero
/// usable listings, which is already a valid, explicit "no market value" result.</summary>
public sealed class EvidenceBasedAssetValuationService : IAssetValuationService
{
    private readonly IMarketDataService _marketDataService;
    private readonly AiEstimatedValuationService _fallback;
    private readonly TimeProvider _timeProvider;

    public EvidenceBasedAssetValuationService(
        IMarketDataService marketDataService,
        AiEstimatedValuationService fallback,
        TimeProvider? timeProvider = null
    )
    {
        _marketDataService = marketDataService;
        _fallback = fallback;
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
        catch (EbayMarketDataException)
        {
            return await _fallback.EstimateAsync(label, brand, model, cancellationToken);
        }
    }
}
