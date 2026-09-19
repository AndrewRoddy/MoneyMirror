namespace MoneyMirror.Data.Entities;

public class FinancialAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string AccountType { get; set; } = string.Empty;
    public decimal CurrentBalance { get; set; }
    public string? InstitutionName { get; set; }
    public DateTimeOffset AsOfDate { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
