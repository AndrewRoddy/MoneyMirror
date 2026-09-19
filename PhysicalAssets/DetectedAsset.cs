namespace MoneyMirror.PhysicalAssets;

/// <summary>
/// One object found in a photo: a generic label plus, where a specific
/// product/model could be inferred, an <see cref="AssetIdentification"/>.
/// </summary>
public record DetectedAsset(
    string Label,
    double Confidence,
    BoundingBox? Region,
    AssetIdentification? Identification);

