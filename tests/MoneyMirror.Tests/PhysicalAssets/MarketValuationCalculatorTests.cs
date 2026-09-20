using MoneyMirror.PhysicalAssets;

namespace MoneyMirror.Tests.PhysicalAssets;

public class MarketValuationCalculatorTests
{
    private static readonly DateTimeOffset ValuedAt = new(2026, 9, 20, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void NoEvidence_ReturnsUnavailableLowConfidenceValue()
    {
        var result = MarketValuationCalculator.Calculate([], ValuedAt);

        Assert.Null(result.EstimatedValueUsd);
        Assert.True(result.IsLowConfidence);
        Assert.False(result.IsAiEstimated);
        Assert.Empty(result.Evidence);
        Assert.Contains("No usable comparable listings", result.Reasoning);
        Assert.Equal(ValuedAt, result.ValuationDate);
    }

    [Fact]
    public void InvalidListings_DoNotInventAZeroValue()
    {
        var result = MarketValuationCalculator.Calculate([
            new(0, "Market", "Free listing", null),
            new(-10, "Market", "Invalid listing", null),
            new(100, " ", "Missing source", null)
        ], ValuedAt);

        Assert.Null(result.EstimatedValueUsd);
        Assert.True(result.IsLowConfidence);
        Assert.Empty(result.Evidence);
    }

    [Theory]
    [InlineData(1, 100)]
    [InlineData(2, 150)]
    [InlineData(3, 200)]
    [InlineData(4, 250)]
    public void SampleSize_ControlsWarning_AndMedianUsesOnlyEvidence(int count, int expected)
    {
        var evidence = Enumerable.Range(1, count)
            .Select(i => new AssetValuationEvidence(i * 100, "Market", $"Listing {i}", "Used"));

        var result = MarketValuationCalculator.Calculate(evidence.Reverse(), ValuedAt);

        Assert.Equal((decimal)expected, result.EstimatedValueUsd);
        Assert.Equal(count < 3, result.IsLowConfidence);
        Assert.Equal("Market evidence", result.SourceLabel);
        Assert.False(result.IsAiEstimated);
        Assert.Equal(count, result.Evidence.Count);
    }

    [Fact]
    public void DuplicateAndInvalidEvidence_DoNotInflateConfidence_AndInputIsSnapshotted()
    {
        var listing = new AssetValuationEvidence(25, "Market", "Chair", "Used");
        var input = new List<AssetValuationEvidence> { listing, listing, new(-100, "Market", null, null) };

        var result = MarketValuationCalculator.Calculate(input, ValuedAt);
        input.Clear();

        Assert.Equal(25m, result.EstimatedValueUsd);
        Assert.True(result.IsLowConfidence);
        Assert.Equal(listing, Assert.Single(result.Evidence));
    }

    [Fact]
    public void EvenMedian_RoundsHalfCentAwayFromZero()
    {
        var result = MarketValuationCalculator.Calculate([
            new(10.01m, "Market", "First", null), new(10.02m, "Market", "Second", null)
        ], ValuedAt);

        Assert.Equal(10.02m, result.EstimatedValueUsd);
    }

    [Fact]
    public void LargePrices_DoNotOverflowDuringAveraging()
    {
        var result = MarketValuationCalculator.Calculate([
            new(decimal.MaxValue, "Market", "First", null),
            new(decimal.MaxValue, "Market", "Second", null)
        ], ValuedAt);

        Assert.Equal(decimal.MaxValue, result.EstimatedValueUsd);
    }
}
