namespace PittMoney.Data.Entities;

public class Skill
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public Guid ProfessionalProfileId { get; set; }
	public string Name { get; set; } = string.Empty;
	public string? ProficiencyLevel { get; set; }
	public int? YearsOfExperience { get; set; }

	public ProfessionalProfile ProfessionalProfile { get; set; } = null!;
}