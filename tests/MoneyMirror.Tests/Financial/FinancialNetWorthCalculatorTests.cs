using MoneyMirror.Financial;

namespace MoneyMirror.Tests.Financial;

public class FinancialNetWorthCalculatorTests
{
    [Fact]
    public void Calculate_SubtractsLiabilitiesFromAssets()
    {
        var result = FinancialNetWorthCalculator.Calculate(
            [8200m, 18400m, 51200m],
            [12400m, 1800m]);

        Assert.Equal(63600m, result);
    }

    [Fact]
    public void Calculate_EmptyCollections_ReturnsZero()
    {
        var result = FinancialNetWorthCalculator.Calculate([], []);

        Assert.Equal(0m, result);
    }

    [Fact]
    public void Calculate_HigherLiabilitiesProducesNegativeNetWorth()
    {
        var result = FinancialNetWorthCalculator.Calculate([1000m], [2500m]);

        Assert.Equal(-1500m, result);
    }
}
