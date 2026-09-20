namespace MoneyMirror.HumanCapital;

/// <summary>
/// Thrown when the BLS API is unreachable, returns an error, or returns a
/// response <see cref="IBlsWageDataService"/> cannot use. Callers should
/// catch this rather than letting provider failures crash the request.
/// </summary>
public class BlsWageDataException : Exception
{
    public BlsWageDataException(string message)
        : base(message) { }

    public BlsWageDataException(string message, Exception innerException)
        : base(message, innerException) { }
}
