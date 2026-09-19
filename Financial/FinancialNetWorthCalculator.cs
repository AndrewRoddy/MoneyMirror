namespace MoneyMirror.Financial;

/// <summary>
/// Performs the deterministic financial net-worth calculation. AI services
/// must not participate in this calculation.
/// </summary>
public static class FinancialNetWorthCalculator
{
    public static decimal Calculate(
        IEnumerable<decimal> assetValues,
        IEnumerable<decimal> liabilityBalances)
    {
        ArgumentNullException.ThrowIfNull(assetValues);
        ArgumentNullException.ThrowIfNull(liabilityBalances);

        return assetValues.Sum() - liabilityBalances.Sum();
    }
}
