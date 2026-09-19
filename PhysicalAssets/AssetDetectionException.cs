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
}

