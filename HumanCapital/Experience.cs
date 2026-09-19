namespace MoneyMirror.HumanCapital;

/// <summary>One employment history entry from a resume.</summary>
public record Experience(
    string Organization,
    string? Title,
    string? StartDate,
    string? EndDate,
    string? Description);
