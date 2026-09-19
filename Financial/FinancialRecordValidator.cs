namespace MoneyMirror.Financial;

/// <summary>
/// Server-side validation for financial records before they are persisted.
/// </summary>
public static class FinancialRecordValidator
{
    public static IReadOnlyList<string> Validate(
        string? type,
        string? name,
        decimal amount,
        DateTime? asOfDate)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(type))
        {
            errors.Add("Choose a type.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            errors.Add("Enter a name.");
        }

        if (amount < 0)
        {
            errors.Add("Amount must be zero or more.");
        }

        if (asOfDate is null)
        {
            errors.Add("Choose an as-of date.");
        }

        return errors;
    }
}
