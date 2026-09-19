namespace PittMoney.HumanCapital;

/// <summary>
/// Thrown when a resume document's file type isn't supported, or the
/// document can't be parsed. Callers should catch this and surface a
/// useful message rather than letting the request crash.
/// </summary>
public class ResumeTextExtractionException : Exception
{
    public ResumeTextExtractionException(string message) : base(message)
    {
    }

    public ResumeTextExtractionException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
