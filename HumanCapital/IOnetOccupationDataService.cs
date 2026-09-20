namespace MoneyMirror.HumanCapital;

/// <summary>
/// Retrieves raw occupation information from O*NET Web Services. Matching and
/// ranking that information is deliberately left to HC4 (#93).
/// </summary>
public interface IOnetOccupationDataService
{
    /// <summary>Gets the O*NET occupation report overview for an O*NET-SOC code.</summary>
    Task<OnetOccupation> GetOccupationAsync(
        string onetSocCode,
        CancellationToken cancellationToken = default
    );
}
