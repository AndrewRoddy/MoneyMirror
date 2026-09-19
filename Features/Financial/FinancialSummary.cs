namespace PittMoney.Features.Financial;

/// <summary>
/// Net-worth figures: sum(assets) - sum(liabilities). Pure C#, no I/O, no AI.
/// Reused by the dashboard (IE1); PA9 extends it with a physical-asset total.
/// </summary>
public record FinancialSummary(decimal TotalAssets, decimal TotalLiabilities)
{
    public decimal NetWorth => TotalAssets - TotalLiabilities;

    public static FinancialSummary Calculate(IEnumerable<FinancialEntry> entries)
    {
        decimal assets = 0;
        decimal liabilities = 0;

        foreach (var entry in entries)
        {
            if (entry.Type == FinancialEntryType.Asset)
            {
                assets += entry.Amount;
            }
            else
            {
                liabilities += entry.Amount;
            }
        }

        return new FinancialSummary(assets, liabilities);
    }
}
