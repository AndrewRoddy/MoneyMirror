namespace MoneyMirror.HumanCapital;

/// <summary>
/// Retrieves real wage/compensation time-series data from the BLS (Bureau
/// of Labor Statistics) Public Data API - the grounded data source behind
/// HC5, replacing #148's AI-estimated compensation MVP placeholder. Returns
/// raw observations only; #97 does the deterministic range aggregation and
/// #99 the LLM explanation - no business logic lives here.
/// </summary>
public interface IBlsWageDataService
{
    /// <summary>
    /// Retrieves observations for the given BLS series IDs across the given
    /// year range. Series IDs are the caller's responsibility (e.g. from an
    /// occupation-to-series mapping) - this service does not resolve
    /// occupation names to series IDs.
    /// </summary>
    /// <exception cref="BlsWageDataException">
    /// The API call failed, or its response couldn't be parsed.
    /// </exception>
    Task<IReadOnlyList<BlsWageObservation>> GetSeriesDataAsync(
        IReadOnlyList<string> seriesIds,
        int startYear,
        int endYear,
        CancellationToken cancellationToken = default);
}
