using MoneyMirror.HumanCapital;

namespace MoneyMirror.Tests.HumanCapital;

public class MarketPotentialEstimateTests
{
    [Fact]
    public void Composition_FromRealObservationsAndAggregatedRange_CarriesEvidenceThrough()
    {
        IReadOnlyList<BlsWageObservation> observations =
        [
            new("OEUS000000000000000000000000001", "2023", "A01", "Annual", 45000m, []),
            new("OEUS000000000000000000000000001", "2023", "A01", "Annual", 85000m, []),
        ];

        var range = BlsCompensationAggregator.Aggregate(observations);
        Assert.NotNull(range);

        var estimate = new MarketPotentialEstimate(
            "Software Engineer",
            ["OEUS000000000000000000000000001"],
            range!,
            2023,
            2023,
            observations
        );

        Assert.Equal("Software Engineer", estimate.Occupation);
        Assert.Equal(45000m, estimate.Range.MinUsd);
        Assert.Equal(85000m, estimate.Range.MaxUsd);
        Assert.Equal(2, estimate.Range.ObservationCount);
        Assert.Same(observations, estimate.Evidence);
        Assert.Equal(["OEUS000000000000000000000000001"], estimate.SeriesIds);
        Assert.Equal(2023, estimate.StartYear);
        Assert.Equal(2023, estimate.EndYear);
    }
}
