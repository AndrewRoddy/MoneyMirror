namespace MoneyMirror.HumanCapital;

/// <summary>
/// Thrown when a downstream call in <see cref="IMarketPotentialPipeline"/>
/// (BLS or the LLM) fails outright. Not used for the insufficient-data
/// case - that's an explicit null result, never an exception.
/// </summary>
public class MarketPotentialPipelineException : Exception
{
    public MarketPotentialPipelineException(string message)
        : base(message) { }

    public MarketPotentialPipelineException(string message, Exception innerException)
        : base(message, innerException) { }
}
