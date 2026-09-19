namespace MoneyMirror.HumanCapital;

/// <summary>
/// Estimates a compensation range from a set of matched occupations. The
/// current implementation (#148) is an explicit MVP placeholder that asks
/// the LLM to guess a plausible range - it is not grounded in a real
/// wage-data source. It exists so the app has an end-to-end demo path
/// before #24 (real wage-data retrieval + deterministic aggregation) is
/// built; see #148 for the swap-out plan.
/// </summary>
public interface ICompensationEstimationService
{
    /// <exception cref="CompensationEstimationException">
    /// The LLM call failed, or its response couldn't be parsed.
    /// </exception>
    Task<CompensationEstimate> EstimateAsync(
        IReadOnlyList<OccupationMatch> occupations,
        CancellationToken cancellationToken = default);
}
