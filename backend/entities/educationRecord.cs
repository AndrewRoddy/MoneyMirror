namespace PittMoney.Data.Entities;

public class EducationRecord
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public Guid ProfessionalProfileId { get; set; }
	public string InstitutionName { get; set; } = string.Empty;
	public string? Degree { get; set; }
	public string? FieldOfStudy { get; set; }
	public DateTimeOffset? StartedAt { get; set; }
	public DateTimeOffset? CompletedAt { get; set; }

	public ProfessionalProfile ProfessionalProfile { get; set; } = null!;
}