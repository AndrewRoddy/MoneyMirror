namespace MoneyMirror.Features.Financial;

public enum FinancialEntryType
{
    Asset,
    Liability,
}

/// <summary>
/// A single manually-tracked financial line item (bank account, investment,
/// loan, credit card, ...). Amount is always a positive magnitude;
/// <see cref="Type"/> decides which side of the net-worth equation it lands on.
/// </summary>
public class FinancialEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public FinancialEntryType Type { get; set; }

    /// <summary>User-facing bucket, e.g. "Cash", "Mortgage".</summary>
    public string Category { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime? AsOfDate { get; set; }
}
