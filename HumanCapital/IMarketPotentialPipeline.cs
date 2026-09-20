namespace MoneyMirror.HumanCapital;

/// <summary>
/// Orchestrates the real (non-AI-estimated) Market Potential compensation
/// pipeline end to end: retrieves real BLS wage observations (#96),
/// aggregates them deterministically into a range (#97), packages the
/// range with its evidence (#98), and has Nemotron explain the result in
/// prose (#99). No AI participates in choosing the numbers - only in
/// describing them afterward.
/// </summary>
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
