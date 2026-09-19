using PittMoney.Features.Financial;

namespace PittMoney.Tests.Financial;

public class FinancialSummaryTests
{
    [Fact]
    public void Calculate_NoEntries_AllZero()
    {
        var summary = FinancialSummary.Calculate([]);

        Assert.Equal(0m, summary.TotalAssets);
        Assert.Equal(0m, summary.TotalLiabilities);
        Assert.Equal(0m, summary.NetWorth);
    }

    [Fact]
    public void Calculate_AssetsAndLiabilities_SumsEachSideAndSubtracts()
    {
        var entries = new[]
        {
            Entry(FinancialEntryType.Asset, 1500.25m),
            Entry(FinancialEntryType.Asset, 10000m),
            Entry(FinancialEntryType.Liability, 8000m),
            Entry(FinancialEntryType.Liability, 250.75m),
        };

        var summary = FinancialSummary.Calculate(entries);

        Assert.Equal(11500.25m, summary.TotalAssets);
        Assert.Equal(8250.75m, summary.TotalLiabilities);
        Assert.Equal(3249.50m, summary.NetWorth);
    }

    [Fact]
    public void Calculate_LiabilitiesExceedAssets_NetIsNegative()
    {
        var entries = new[]
        {
            Entry(FinancialEntryType.Asset, 100m),
            Entry(FinancialEntryType.Liability, 5000m),
        };

        Assert.Equal(-4900m, FinancialSummary.Calculate(entries).NetWorth);
    }

    [Fact]
    public void Calculate_IsDeterministic_SameInputSameOutput()
    {
        var entries = new[]
        {
            Entry(FinancialEntryType.Asset, 3m),
            Entry(FinancialEntryType.Liability, 1m),
        };

        Assert.Equal(FinancialSummary.Calculate(entries), FinancialSummary.Calculate(entries));
    }

    private static FinancialEntry Entry(FinancialEntryType type, decimal amount) =>
        new()
        {
            Type = type,
            Name = "x",
            Amount = amount,
        };
}
