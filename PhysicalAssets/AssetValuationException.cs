namespace MoneyMirror.PhysicalAssets;

/// <summary>
/// Thrown when the LLM call for a valuation estimate fails, or its response
/// can't be parsed. Callers should catch this rather than letting the
/// failure crash the request.
/// </summary>
public class AssetValuationException : Exception
{
    public AssetValuationException(string message) : base(message)
    {
    }

    public AssetValuationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

