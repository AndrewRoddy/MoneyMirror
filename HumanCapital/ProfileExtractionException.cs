namespace MoneyMirror.HumanCapital;

/// <summary>
/// Thrown when the LLM call for profile extraction fails, or returns a
/// response that can't be parsed into a <see cref="ProfessionalProfile"/>.
/// This is the "useful error state" required by #21 instead of a crash.
/// </summary>
public class ProfileExtractionException : Exception
{
    public ProfileExtractionException(string message) : base(message)
    {
    }

    public ProfileExtractionException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

