namespace MoneyMirror.HumanCapital;

/// <summary>
/// Matches a professional profile to source-backed occupations and ranks them
/// by overlap between profile evidence and occupation data.
/// </summary>
public interface IOccupationMatchingService
{
    /// <exception cref="OccupationMatchingException">
    /// The occupation data provider failed or returned unusable data.
    /// </exception>
    Task<IReadOnlyList<OccupationMatch>> MatchAsync(
        ProfessionalProfile profile,
        CancellationToken cancellationToken = default);
}
