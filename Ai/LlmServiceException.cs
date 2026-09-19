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
}
