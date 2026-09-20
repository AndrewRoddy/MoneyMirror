namespace MoneyMirror.HumanCapital;

/// <summary>
/// Adds an LLM-authored prose explanation to a BLS-grounded
/// <see cref="MarketPotentialEstimate"/> (#96/#97/#98). The LLM explains
/// the reasoning behind an already-computed, deterministic range - it
/// never sets or adjusts the numbers themselves.
/// </summary>
public interface IMarketPotentialExplanationService
{
    /// <exception cref="MarketPotentialExplanationException">
    /// The LLM provider call failed, or returned an unusable response.
    /// </exception>
    Task<string> ExplainAsync(MarketPotentialEstimate estimate, CancellationToken cancellationToken = default);
}
