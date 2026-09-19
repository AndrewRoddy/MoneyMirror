namespace MoneyMirror.HumanCapital;

/// <summary>
/// Persists an extracted/reviewed <see cref="ProfessionalProfile"/> linked
/// to the single default profile record.
/// </summary>
public interface IProfessionalProfileRepository
{
    /// <summary>
    /// Loads the default profile, or <see cref="ProfessionalProfile.Empty"/>
    /// if nothing has been saved yet.
    /// </summary>
    Task<ProfessionalProfile> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves <paramref name="profile"/>, replacing any previously stored
    /// sections for the default profile.
    /// </summary>
    Task SaveAsync(ProfessionalProfile profile, CancellationToken cancellationToken = default);
}
