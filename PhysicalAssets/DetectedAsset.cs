namespace MoneyMirror.PhysicalAssets;

/// <summary>
/// A single point in normalized image coordinates (0.0 to 1.0).
/// </summary>
public record NormalizedPoint(double X, double Y);

/// <summary>
/// One object found in a photo: a generic label plus, where a specific
/// product/model could be inferred, an <see cref="AssetIdentification"/>.
/// Tags describe visible, useful search facets such as color, material, or
/// texture; they are observations, not an inferred product identification.
/// Mask provides optional polygon segmentation points in normalized coordinates.
/// </summary>
public record DetectedAsset(
    string Label,
    double Confidence,
    BoundingBox? Region,
    AssetIdentification? Identification,
    IReadOnlyList<string> Tags,
    IReadOnlyList<NormalizedPoint>? Mask = null);
