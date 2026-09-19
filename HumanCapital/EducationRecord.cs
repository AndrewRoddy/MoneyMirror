namespace MoneyMirror.HumanCapital;

/// <summary>One institution/program entry from a resume's education section.</summary>
public record EducationRecord(
    string Institution,
    string? Degree,
    string? FieldOfStudy,
    string? StartDate,
    string? EndDate);
