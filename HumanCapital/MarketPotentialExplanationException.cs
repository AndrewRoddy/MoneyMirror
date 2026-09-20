namespace MoneyMirror.HumanCapital;

/// <summary>
/// Thrown when the LLM call to explain a <see cref="MarketPotentialEstimate"/>
/// fails, or returns an unusable response. Callers should catch this rather
/// than letting provider failures crash the request.
/// </summary>
public class MarketPotentialExplanationException : Exception
{
    public MarketPotentialExplanationException(string message)
        : base(message) { }

    public MarketPotentialExplanationException(string message, Exception innerException)
        : base(message, innerException) { }
}
