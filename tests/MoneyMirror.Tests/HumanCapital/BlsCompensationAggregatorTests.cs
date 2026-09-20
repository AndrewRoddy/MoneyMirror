using MoneyMirror.HumanCapital;

namespace MoneyMirror.Tests.HumanCapital;

public class BlsCompensationAggregatorTests
{
    private static BlsWageObservation Observation(decimal? value) =>
        new("S1", "2023", "A01", "Annual", value, []);

    [Fact]
    public void Aggregate_MultipleObservations_ReturnsMinAndMax()
    {
        var range = BlsCompensationAggregator.Aggregate(
            [Observation(45000m), Observation(85000m), Observation(62000m)]
        );

        Assert.NotNull(range);
        Assert.Equal(45000m, range!.MinUsd);
        Assert.Equal(85000m, range.MaxUsd);
        Assert.Equal(3, range.ObservationCount);
    }

    [Fact]
    public void Aggregate_SingleObservation_MinEqualsMax()
    {
        var range = BlsCompensationAggregator.Aggregate([Observation(60000m)]);

        Assert.NotNull(range);
        Assert.Equal(60000m, range!.MinUsd);
        Assert.Equal(60000m, range.MaxUsd);
        Assert.Equal(1, range.ObservationCount);
    }

    [Fact]
    public void Aggregate_MixOfNullAndRealValues_IgnoresNulls()
    {
        var range = BlsCompensationAggregator.Aggregate(
            [Observation(null), Observation(50000m), Observation(null), Observation(70000m)]
        );

        Assert.NotNull(range);
        Assert.Equal(50000m, range!.MinUsd);
        Assert.Equal(70000m, range.MaxUsd);
        Assert.Equal(2, range.ObservationCount);
    }

    [Fact]
    public void Aggregate_AllValuesSuppressed_ReturnsNullRatherThanFabricatingARange()
    {
        var range = BlsCompensationAggregator.Aggregate([Observation(null), Observation(null)]);

        Assert.Null(range);
    }

    [Fact]
    public void Aggregate_EmptyList_ReturnsNull()
    {
        var range = BlsCompensationAggregator.Aggregate([]);

        Assert.Null(range);
    }
}
