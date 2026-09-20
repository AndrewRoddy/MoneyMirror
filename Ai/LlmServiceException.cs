namespace MoneyMirror.Ai;

/// <summary>
/// Thrown when the LLM provider is unreachable, returns an error, or returns a
/// response <see cref="ILlmService"/> cannot use. Callers should catch this rather
/// than letting provider failures crash the request.
/// </summary>
public class LlmServiceException : Exception
{
    public LlmServiceException(string message)
        : base(message) { }

    public LlmServiceException(string message, Exception innerException)
        : base(message, innerException) { }

    /// <summary>
    /// The provider's verbatim reply, when there was one: an error body such as
    /// NVIDIA's "ResourceExhausted: Worker local total request limit reached",
    /// or a completion that could not be used. Null when the call never got a
    /// response at all. Surfaced in the UI so a transient provider outage is
    /// distinguishable from a fault in this app.
    /// </summary>
    public string? RawResponse { get; init; }
}
