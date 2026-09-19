namespace PittMoney.Ai;

/// <summary>
/// Thrown when the vision model provider is unreachable, returns an error, or
/// returns a response <see cref="IVisionService"/> cannot use. Callers should
/// catch this rather than letting provider failures crash the request.
/// </summary>
public class VisionServiceException : Exception
{
    public VisionServiceException(string message) : base(message)
    {
    }

    public VisionServiceException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
