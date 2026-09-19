namespace PittMoney.Data.Entities;

public class Certification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProfessionalProfileId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? IssuingOrganization { get; set; }
    public string? CredentialId { get; set; }
    public DateTimeOffset? IssuedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }

    public ProfessionalProfile ProfessionalProfile { get; set; } = null!;
}
