namespace MoneyMirror.PhysicalAssets;

/// <summary>
/// Finds the distinct objects in a photo and, where possible, identifies
/// the likely product/model for each. Pure recognition - no valuation
/// logic here.
/// </summary>
public interface IPhysicalAssetDetectionService
{
    /// <summary>
    /// Detects objects in <paramref name="imageBytes"/> (of type
    /// <paramref name="mediaType"/>, e.g. "image/jpeg"). Handles both
    /// single-item and multi-item (room) photos - the result has one
    /// entry per object found, possibly just one.
    /// </summary>
    /// <exception cref="AssetDetectionException">
    /// The vision call failed, or its response couldn't be parsed.
    /// </exception>
    Task<IReadOnlyList<DetectedAsset>> DetectAsync(
        byte[] imageBytes,
        string mediaType,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Identifies a single physical possession from an isolated cutout image (background stripped).
    /// Focuses on brand, model/flavor/edition, condition, and category tags with high precision.
    /// </summary>
    Task<DetectedAsset> IdentifyCutoutAsync(
        byte[] imageBytes,
        string mediaType,
        CancellationToken cancellationToken = default
    );
}
