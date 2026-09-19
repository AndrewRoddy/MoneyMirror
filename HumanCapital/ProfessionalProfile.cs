namespace PittMoney.HumanCapital;

/// <summary>
/// A structured professional profile extracted from a resume. Sections with
/// nothing found in the source document are empty collections, never
/// fabricated entries.
/// </summary>
public record ProfessionalProfile(
    IReadOnlyList<EducationRecord> Education,
    IReadOnlyList<Certification> Certifications,
    IReadOnlyList<Skill> Skills,
    IReadOnlyList<Experience> Experience,
    IReadOnlyList<Project> Projects,
    IReadOnlyList<Publication> Publications,
    IReadOnlyList<Award> Awards)
{
    public static ProfessionalProfile Empty { get; } = new(
        Education: [],
        Certifications: [],
        Skills: [],
        Experience: [],
        Projects: [],
        Publications: [],
        Awards: []);
}
