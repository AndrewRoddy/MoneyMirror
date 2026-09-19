namespace PittMoney.HumanCapital;

/// <summary>
/// Thrown when the LLM call for occupation matching fails, or its response
/// can't be parsed. Callers should catch this rather than letting the
/// failure crash the request.
/// </summary>
public class OccupationMatchingException : Exception
{
    public OccupationMatchingException(string message) : base(message)
    {
    }

    public OccupationMatchingException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
