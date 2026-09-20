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

    /// <summary>
    /// The provider's verbatim reply, when there was one: an error body such as
    /// NVIDIA's "ResourceExhausted: Worker local total request limit reached",
    /// or a completion that could not be used. Null when the call never got a
    /// response at all. Surfaced in the UI so a transient provider outage is
    /// distinguishable from a fault in this app.
    /// </summary>
    public string? RawResponse { get; init; }
}

