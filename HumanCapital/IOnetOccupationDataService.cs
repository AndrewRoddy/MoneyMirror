namespace MoneyMirror.HumanCapital;

/// <summary>
/// Retrieves raw occupation information from O*NET Web Services.
/// </summary>
public interface IOnetOccupationDataService
{
    /// <summary>Searches O*NET's occupation database using a profile keyword or phrase.</summary>
    Task<IReadOnlyList<OnetOccupationSearchResult>> SearchOccupationsAsync(
        string keyword,
        CancellationToken cancellationToken = default
    );

    /// <summary>Gets O*NET's top occupation-specific skill descriptors.</summary>
    Task<IReadOnlyList<string>> GetOccupationSkillsAsync(
        string onetSocCode,
        CancellationToken cancellationToken = default
    );

    /// <summary>Gets the O*NET occupation report overview for an O*NET-SOC code.</summary>
    Task<OnetOccupation> GetOccupationAsync(
        string onetSocCode,
        CancellationToken cancellationToken = default
    );
}
