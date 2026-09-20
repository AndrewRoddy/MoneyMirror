namespace MoneyMirror.PhysicalAssets;

/// <summary>
/// One object found in a photo: a generic label plus, where a specific
/// product/model could be inferred, an <see cref="AssetIdentification"/>.
/// Tags describe visible, useful search facets such as color, material, or
/// texture; they are observations, not an inferred product identification.
/// </summary>
public record DetectedAsset(
    string Label,
    double Confidence,
    BoundingBox? Region,
    AssetIdentification? Identification,
    IReadOnlyList<string> Tags);
