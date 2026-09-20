namespace MoneyMirror.PhysicalAssets;

/// <summary>
/// An estimated resale value for a physical asset.
/// </summary>
/// <param name="IsAiEstimated">
/// True when the value came directly from the LLM's
/// judgment, not from real market comps. Never present this to the user
/// (or persist it) as if it were evidence-based - #138 is a deliberate,
/// temporary stand-in for the real PA5 (#14) -&gt; PA6 (#68-71) pipeline,
/// which aggregates real comps deterministically and sets this false.
/// </param>
public record AssetValuation(
    decimal? EstimatedValueUsd,
    string Reasoning,
    DateTimeOffset ValuationDate,
    bool IsAiEstimated)
{
    /// <summary>AI estimates and fewer than three usable comps are low confidence.
    /// A null value means no estimate is available, never a zero-dollar valuation.</summary>
    public bool IsLowConfidence => IsAiEstimated || EstimatedValueUsd is null
        || Evidence.Count < MarketValuationCalculator.MinimumComparableCount;

    public string SourceLabel => IsAiEstimated
        ? "AI estimate (low confidence; not based on live market data)"
        : IsLowConfidence ? "Market evidence (low confidence)" : "Market evidence";

    /// <summary>
    /// Comparable market listings used to derive this valuation. This is empty
    /// for estimates that are not grounded in listing evidence.
    /// </summary>
    public IReadOnlyList<AssetValuationEvidence> Evidence { get; init; } = Array.Empty<AssetValuationEvidence>();
}

