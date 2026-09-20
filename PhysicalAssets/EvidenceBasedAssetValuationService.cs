namespace MoneyMirror.PhysicalAssets;

/// <summary>Builds an asset valuation from structured market listings, without an LLM.
/// Falls back to <see cref="AiEstimatedValuationService"/>'s price guess whenever eBay
/// itself does not produce a usable value - either it fails outright (credentials,
/// network, rate limit) or it succeeds with zero usable comparable listings. Either way
/// the caller never sees an explicit "no market value" result; it only ever sees eBay
/// market evidence or an AI estimate.</summary>
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
        AssetValuation? marketValuation = null;
        try
        {
            var evidence = await _marketDataService.SearchUsedListingsAsync(
                label,
                brand,
                model,
                cancellationToken
            );
            marketValuation = MarketValuationCalculator.Calculate(evidence, _timeProvider.GetUtcNow());
        }
        catch (EbayMarketDataException)
        {
            // Fall through to the AI estimate below.
        }

        return marketValuation?.EstimatedValueUsd is not null
            ? marketValuation
            : await _fallback.EstimateAsync(label, brand, model, cancellationToken);
    }
}
