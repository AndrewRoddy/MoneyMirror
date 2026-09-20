namespace MoneyMirror.HumanCapital;

/// <summary>
/// <see cref="IMarketPotentialPipeline"/> implementation chaining the real
/// pieces together: #96 -> #97 -> #98 -> #99.
/// </summary>
public class MarketPotentialPipeline : IMarketPotentialPipeline
{
    private readonly IBlsWageDataService _wageDataService;
    private readonly IMarketPotentialExplanationService _explanationService;

    public MarketPotentialPipeline(
        IBlsWageDataService wageDataService,
        IMarketPotentialExplanationService explanationService
    )
    {
        _wageDataService = wageDataService;
        _explanationService = explanationService;
    }

    public async Task<MarketPotentialResult?> GetCompensationAsync(
        string occupation,
        IReadOnlyList<string> seriesIds,
        int startYear,
        int endYear,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(occupation);
        ArgumentNullException.ThrowIfNull(seriesIds);

        IReadOnlyList<BlsWageObservation> observations;
        try
        {
            observations = await _wageDataService.GetSeriesDataAsync(seriesIds, startYear, endYear, cancellationToken);
        }
        catch (BlsWageDataException ex)
        {
            throw new MarketPotentialPipelineException("Failed to retrieve BLS wage data.", ex);
        }

        // #24's acceptance criteria: insufficient data is an explicit
        // result, never a fabricated range - mirrors PA5.3/PA6.4's
        // no-comps-found handling for physical-asset valuation.
        var range = BlsCompensationAggregator.Aggregate(observations);
        if (range is null)
        {
            return null;
        }

        var estimate = new MarketPotentialEstimate(occupation, seriesIds, range, startYear, endYear, observations);

        string explanation;
        try
        {
            explanation = await _explanationService.ExplainAsync(estimate, cancellationToken);
        }
        catch (MarketPotentialExplanationException ex)
        {
            throw new MarketPotentialPipelineException("Failed to explain the compensation estimate.", ex);
        }

        return new MarketPotentialResult(estimate, explanation);
    }
}
