namespace MoneyMirror.HumanCapital;

/// <summary>
/// The AI-derived occupation matches and compensation estimate for the
/// current profile, plus the profile they were derived from.
/// </summary>
public sealed record MarketPotentialSummary(
    ProfessionalProfile Profile,
    bool HasProfile,
    IReadOnlyList<OccupationMatch> Occupations,
    CompensationEstimate? Compensation
);

/// <summary>
/// Produces the <see cref="MarketPotentialSummary"/> that both the Dashboard
/// and Market Potential page display, combining <see cref="IProfessionalProfileRepository"/>,
/// <see cref="IOccupationMatchingService"/>, and <see cref="ICompensationEstimationService"/>
/// behind a single call.
/// </summary>
public interface IMarketPotentialSummaryService
{
    /// <exception cref="OccupationMatchingException">
    /// The occupation-matching LLM call failed, or its response couldn't be parsed.
    /// </exception>
    /// <exception cref="CompensationEstimationException">
    /// The compensation-estimation LLM call failed, or its response couldn't be parsed.
    /// </exception>
    Task<MarketPotentialSummary> GetSummaryAsync(CancellationToken cancellationToken = default);
}
