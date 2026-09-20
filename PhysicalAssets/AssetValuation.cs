namespace MoneyMirror.PhysicalAssets;

/// <summary>
/// An estimated resale value for a physical asset.
/// </summary>
/// <param name="IsAiEstimated">
/// True for this MVP placeholder: the value came directly from the LLM's
/// judgment, not from real market comps. Never present this to the user
/// (or persist it) as if it were evidence-based - #138 is a deliberate,
/// temporary stand-in for the real PA5 (#14) -&gt; PA6 (#68-71) pipeline,
/// which aggregates real comps deterministically and sets this false.
/// </param>
public record AssetValuation(
    decimal EstimatedValueUsd,
    string Reasoning,
    DateTimeOffset ValuationDate,
    bool IsAiEstimated)
{
    /// <summary>
    /// Comparable market listings used to derive this valuation. This is empty
    /// for estimates that are not grounded in listing evidence.
    /// </summary>
    public IReadOnlyList<AssetValuationEvidence> Evidence { get; init; } = Array.Empty<AssetValuationEvidence>();
}

