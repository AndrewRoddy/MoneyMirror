using MoneyMirror.HumanCapital;

namespace MoneyMirror.Tests.HumanCapital;

public class MarketPotentialPipelineTests
{
    private class FakeBlsWageDataService(IReadOnlyList<BlsWageObservation> observations) : IBlsWageDataService
    {
        public Task<IReadOnlyList<BlsWageObservation>> GetSeriesDataAsync(
            IReadOnlyList<string> seriesIds,
            int startYear,
            int endYear,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(observations);
    }

    private class ThrowingBlsWageDataService : IBlsWageDataService
    {
        public Task<IReadOnlyList<BlsWageObservation>> GetSeriesDataAsync(
            IReadOnlyList<string> seriesIds,
            int startYear,
            int endYear,
            CancellationToken cancellationToken = default
        ) => throw new BlsWageDataException("BLS unavailable");
    }

    private class FakeExplanationService(string explanation) : IMarketPotentialExplanationService
    {
        public MarketPotentialEstimate? LastEstimate { get; private set; }

        public Task<string> ExplainAsync(MarketPotentialEstimate estimate, CancellationToken cancellationToken = default)
        {
            LastEstimate = estimate;
            return Task.FromResult(explanation);
        }
    }

    private class ThrowingExplanationService : IMarketPotentialExplanationService
    {
        public Task<string> ExplainAsync(MarketPotentialEstimate estimate, CancellationToken cancellationToken = default) =>
            throw new MarketPotentialExplanationException("LLM unavailable");
    }

    private static readonly IReadOnlyList<BlsWageObservation> SampleObservations =
    [
        new("S1", "2023", "A01", "Annual", 85000m, []),
        new("S1", "2023", "A01", "Annual", 130000m, []),
    ];

    [Fact]
    public async Task GetCompensationAsync_HappyPath_ChainsAllFourStepsCorrectly()
    {
        var explanationService = new FakeExplanationService("This range reflects real BLS wage data.");
        var pipeline = new MarketPotentialPipeline(new FakeBlsWageDataService(SampleObservations), explanationService);

        var result = await pipeline.GetCompensationAsync("Software Engineer", ["S1"], 2023, 2023);

        Assert.NotNull(result);
        Assert.Equal("Software Engineer", result!.Estimate.Occupation);
        Assert.Equal(85000m, result.Estimate.Range.MinUsd);
        Assert.Equal(130000m, result.Estimate.Range.MaxUsd);
        Assert.Equal(2, result.Estimate.Range.ObservationCount);
        Assert.Same(SampleObservations, result.Estimate.Evidence);
        Assert.Equal("This range reflects real BLS wage data.", result.Explanation);
        Assert.Same(result.Estimate, explanationService.LastEstimate);
    }

    [Fact]
    public async Task GetCompensationAsync_NoUsableWageData_ReturnsNullRatherThanFabricating()
    {
        var pipeline = new MarketPotentialPipeline(
            new FakeBlsWageDataService([]),
            new FakeExplanationService("should never be called")
        );

        var result = await pipeline.GetCompensationAsync("Obscure Occupation", ["S1"], 2023, 2023);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetCompensationAsync_AllValuesSuppressed_ReturnsNull()
    {
        IReadOnlyList<BlsWageObservation> suppressed = [new("S1", "2023", "A01", "Annual", null, ["suppressed"])];
        var pipeline = new MarketPotentialPipeline(
            new FakeBlsWageDataService(suppressed),
            new FakeExplanationService("should never be called")
        );

        var result = await pipeline.GetCompensationAsync("Occupation", ["S1"], 2023, 2023);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetCompensationAsync_BlsCallFails_ThrowsMarketPotentialPipelineException()
    {
        var pipeline = new MarketPotentialPipeline(
            new ThrowingBlsWageDataService(),
            new FakeExplanationService("should never be called")
        );

        await Assert.ThrowsAsync<MarketPotentialPipelineException>(
            () => pipeline.GetCompensationAsync("Occupation", ["S1"], 2023, 2023)
        );
    }

    [Fact]
    public async Task GetCompensationAsync_ExplanationCallFails_ThrowsMarketPotentialPipelineException()
    {
        var pipeline = new MarketPotentialPipeline(
            new FakeBlsWageDataService(SampleObservations),
            new ThrowingExplanationService()
        );

        await Assert.ThrowsAsync<MarketPotentialPipelineException>(
            () => pipeline.GetCompensationAsync("Occupation", ["S1"], 2023, 2023)
        );
    }
}
