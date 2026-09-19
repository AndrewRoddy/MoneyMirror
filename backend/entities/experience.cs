namespace MoneyMirror.Data.Entities;

public class Experience
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProfessionalProfileId { get; set; }
    public string EmployerName { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }

    public ProfessionalProfile ProfessionalProfile { get; set; } = null!;
}
