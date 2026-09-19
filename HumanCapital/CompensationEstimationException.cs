namespace MoneyMirror.HumanCapital;

/// <summary>
/// Thrown when the LLM call for a compensation estimate fails, or its
/// response can't be parsed. Callers should catch this rather than letting
/// the failure crash the request.
/// </summary>
public class CompensationEstimationException : Exception
{
    public CompensationEstimationException(string message) : base(message)
    {
    }

    public CompensationEstimationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
