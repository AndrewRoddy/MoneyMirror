namespace MoneyMirror.HumanCapital;

/// <summary>
/// Orchestrates the real (non-AI-estimated) Market Potential compensation
/// pipeline end to end: retrieves real BLS wage observations (#96),
/// aggregates them deterministically into a range (#97), packages the
/// range with its evidence (#98), and has Nemotron explain the result in
/// prose (#99). No AI participates in choosing the numbers - only in
/// describing them afterward.
/// </summary>
/// <remarks>
/// #266: not currently called from any page - <c>/market-potential</c> and
/// the Dashboard still use the AI-guess-only <see cref="ICompensationEstimationService"/>
/// (#148), whose UI explicitly labels its output "AI estimate - not based
/// on live wage data". This isn't an oversight: this method needs a BLS
/// series ID per matched occupation, which was meant to come from O*NET
/// occupation data (#93/#94, HC4) - but eBay and O*NET were both dropped
/// from project scope (see #177) before that occupation-to-series mapping
/// was built. Wiring this in requires deciding how to get that mapping
/// without O*NET (a fixed lookup table for common titles? a second LLM
/// call to guess a series ID, no longer "no AI in choosing the number"?)
/// before this pipeline can replace the AI-guess path in the UI.
/// </remarks>
public interface IMarketPotentialPipeline
{
    /// <returns>
    /// Null if there wasn't enough usable wage data in the given
    /// series/year range to build a range from - an explicit
    /// insufficient-data result, never a fabricated one.
    /// </returns>
    /// <exception cref="MarketPotentialPipelineException">
    /// A downstream API call (BLS or the LLM) failed outright.
    /// </exception>
    Task<MarketPotentialResult?> GetCompensationAsync(
        string occupation,
        IReadOnlyList<string> seriesIds,
        int startYear,
        int endYear,
        CancellationToken cancellationToken = default
    );
}
