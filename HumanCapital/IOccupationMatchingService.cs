namespace MoneyMirror.HumanCapital;

/// <summary>
/// Matches a professional profile to candidate occupations. The current
/// implementation (#142) is an explicit MVP placeholder that asks the LLM
/// to suggest plausible matches - it is not grounded in a real
/// labor-market data source.
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
