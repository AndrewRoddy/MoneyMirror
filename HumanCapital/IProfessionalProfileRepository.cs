namespace MoneyMirror.HumanCapital;

/// <summary>
/// Persists an extracted/reviewed <see cref="ProfessionalProfile"/> linked
/// to the single default profile record.
/// </summary>
public interface IProfessionalProfileRepository
{
    /// <summary>
    /// Saves <paramref name="profile"/>, replacing any previously stored
    /// sections for the default profile.
    /// </summary>
    Task SaveAsync(ProfessionalProfile profile, CancellationToken cancellationToken = default);
}
