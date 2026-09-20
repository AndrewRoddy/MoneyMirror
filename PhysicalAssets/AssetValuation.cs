namespace MoneyMirror.PhysicalAssets;

/// <summary>
/// An estimated resale value for a physical asset.
/// </summary>
/// <param name="IsAiEstimated">
/// True when the value came from an LLM estimate rather than market listings.
/// </param>
public record AssetValuation(
    decimal? EstimatedValueUsd,
    string Reasoning,
    DateTimeOffset ValuationDate,
    bool IsAiEstimated)
{
    /// <summary>AI estimates and fewer than three usable comps are low confidence.
    /// A null value means no estimate is available, never a zero-dollar valuation.
    /// A computed domain fact only - never surfaced in <see cref="SourceLabel"/> or
    /// displayed to the user, who sees one consistent presentation regardless of
    /// provenance.</summary>
    public bool IsLowConfidence => IsAiEstimated || EstimatedValueUsd is null
        || Evidence.Count < MarketValuationCalculator.MinimumComparableCount;

    public string SourceLabel => IsAiEstimated ? "AI estimate" : "Market evidence";

    /// <summary>
    /// Comparable market listings used to derive this valuation. This is empty
    /// for estimates that are not grounded in listing evidence.
    /// </summary>
    public IReadOnlyList<AssetValuationEvidence> Evidence { get; init; } = Array.Empty<AssetValuationEvidence>();
}

