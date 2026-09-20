namespace MoneyMirror.HumanCapital;

/// <summary>
/// Deterministic aggregation of BLS wage observations into a compensation
/// range - no LLM involvement in choosing the numbers. Like
/// <see cref="Financial.FinancialNetWorthCalculator"/> and
/// <see cref="SkillGapAnalyzer"/>, this is plain C#; #99 is where Nemotron
/// gets involved, and only to explain this result in prose, not to set it.
/// </summary>
public static class BlsCompensationAggregator
{
    /// <summary>
    /// Computes the min/max across the given observations' values,
    /// ignoring BLS-suppressed (null) values.
    /// </summary>
    /// <returns>
    /// Null if none of the given observations have a usable value - e.g.
    /// every value was suppressed by BLS, or the list was empty. Never
    /// fabricates a range from no data (mirrors PA5.3's no-comps-found
    /// handling for the physical-asset valuation pipeline).
    /// </returns>
    public static BlsCompensationRange? Aggregate(IEnumerable<BlsWageObservation> observations)
    {
        ArgumentNullException.ThrowIfNull(observations);

        var values = observations.Select(o => o.Value).Where(v => v.HasValue).Select(v => v!.Value).ToList();

        return values.Count == 0 ? null : new BlsCompensationRange(values.Min(), values.Max(), values.Count);
    }
}
