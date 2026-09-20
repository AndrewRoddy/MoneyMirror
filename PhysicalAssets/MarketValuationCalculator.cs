namespace MoneyMirror.PhysicalAssets;

/// <summary>Calculates a USD median from already-matched market listings, without an LLM.
/// The retrieval layer is responsible for currency conversion and matching the item's condition.</summary>
public static class MarketValuationCalculator
{
    // A small-sample warning, not a statistical guarantee of accuracy above this threshold.
    public const int MinimumComparableCount = 3;

    public static AssetValuation Calculate(IEnumerable<AssetValuationEvidence> evidence, DateTimeOffset valuationDate)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        var usable = evidence
            .Where(e => e.PriceUsd > 0 && !string.IsNullOrWhiteSpace(e.Source))
            .Distinct()
            .OrderBy(e => e.PriceUsd)
            .ToArray();

        decimal? value = null;
        string reasoning;
        if (usable.Length == 0)
        {
            reasoning = "No usable comparable listings were found. No market value is available.";
        }
        else
        {
            var middle = usable.Length / 2;
            var median = usable.Length % 2 == 1
                ? usable[middle].PriceUsd
                : usable[middle - 1].PriceUsd + (usable[middle].PriceUsd - usable[middle - 1].PriceUsd) / 2;
            value = decimal.Round(median, 2, MidpointRounding.AwayFromZero);
            reasoning = $"Median of {usable.Length} usable comparable listing(s), rounded to the nearest cent.";
            if (usable.Length < MinimumComparableCount)
            {
                reasoning = $"Based on {usable.Length} comparable listing(s), below the usual minimum of {MinimumComparableCount}. {reasoning}";
            }
        }

        return new AssetValuation(value, reasoning, valuationDate, IsAiEstimated: false)
        {
            Evidence = Array.AsReadOnly(usable)
        };
    }
}
