namespace MoneyMirror.HumanCapital;

/// <summary>
/// Converts raw resume text into a structured <see cref="ProfessionalProfile"/>
/// using the LLM seam. Does no persistence - that's a later stage.
/// </summary>
public interface IProfessionalProfileExtractionService
{
    /// <summary>
    /// Extracts a <see cref="ProfessionalProfile"/> from <paramref name="resumeText"/>.
    /// Returns <see cref="ProfessionalProfile.Empty"/> for blank input.
    /// </summary>
    /// <exception cref="ProfileExtractionException">
    /// The LLM call failed, or its response couldn't be parsed as a profile.
    /// </exception>
    Task<ProfessionalProfile> ExtractAsync(string resumeText, CancellationToken cancellationToken = default);
}
