namespace PittMoney.HumanCapital;

/// <summary>A professional certification or license listed on a resume.</summary>
public record Certification(
    string Name,
    string? IssuingOrganization,
    string? IssueDate);
