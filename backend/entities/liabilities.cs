namespace MoneyMirror.Data.Entities;

public class Liability
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string LiabilityType { get; set; } = string.Empty;
    public decimal OutstandingBalance { get; set; }
    public string? InstitutionName { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
