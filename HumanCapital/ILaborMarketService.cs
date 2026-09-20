namespace MoneyMirror.HumanCapital;

/// <summary>
/// Retrieves occupation data from a labor-market source.
/// </summary>
/// <remarks>
/// This seam intentionally does not match or rank occupations. Those concerns
/// belong to the follow-up HC4 issues and can consume this source-backed data.
/// </remarks>
public interface ILaborMarketService
{
    /// <summary>Gets one occupation by its source-specific code.</summary>
    Task<LaborMarketOccupation> GetOccupationAsync(
        string occupationCode,
        CancellationToken cancellationToken = default
    );
}
