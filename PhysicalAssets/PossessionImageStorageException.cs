namespace MoneyMirror.PhysicalAssets;

/// <summary>
/// Thrown when a possession photo can't be saved to or read from storage.
/// </summary>
public class PossessionImageStorageException : Exception
{
    public PossessionImageStorageException(string message) : base(message)
    {
    }

    public PossessionImageStorageException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

