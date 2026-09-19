namespace PittMoney.HumanCapital;

/// <summary>
/// Matches a professional profile to candidate occupations. The current
/// implementation (#142) is an explicit MVP placeholder that asks the LLM
/// to suggest plausible matches - it is not grounded in a real
/// labor-market data source. It exists so the app has an end-to-end demo
/// path before #23 (ILaborMarketService integration) is built; see #142
/// for the swap-out plan.
/// </summary>
public interface IOccupationMatchingService
{
    /// <exception cref="OccupationMatchingException">
    /// The LLM call failed, or its response couldn't be parsed.
    /// </exception>
    Task<IReadOnlyList<OccupationMatch>> MatchAsync(
        ProfessionalProfile profile,
        CancellationToken cancellationToken = default);
}
