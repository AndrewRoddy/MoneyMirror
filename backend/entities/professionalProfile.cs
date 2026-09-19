namespace PittMoney.Data.Entities;

public class ProfessionalProfile
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public string DisplayName { get; set; } = string.Empty;
	public string? Summary { get; set; }
	public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

	public ICollection<EducationRecord> EducationRecords { get; set; } =
		new List<EducationRecord>();
	public ICollection<Certification> Certifications { get; set; } =
		new List<Certification>();
	public ICollection<Skill> Skills { get; set; } = new List<Skill>();
	public ICollection<Experience> Experiences { get; set; } =
		new List<Experience>();
}