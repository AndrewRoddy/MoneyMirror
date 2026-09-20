namespace MoneyMirror.PhysicalAssets;

/// <summary>
/// Thrown when the vision call for object detection fails, or its response
/// can't be parsed into <see cref="DetectedAsset"/> entries. The "useful
/// error state" required by #12 instead of a crash.
/// </summary>
public class AssetDetectionException : Exception
{
    public AssetDetectionException(string message) : base(message)
    {
    }

    public AssetDetectionException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// The model's verbatim reply, when there was one. The vision model
    /// ignores the "JSON only" instruction often enough that "could not be
    /// parsed" is ambiguous on its own - surfacing the raw text is what
    /// distinguishes a prose answer from a genuine parser bug. Null when the
    /// provider call itself failed and no reply was received.
    /// </summary>
    public string? RawResponse { get; init; }
}

