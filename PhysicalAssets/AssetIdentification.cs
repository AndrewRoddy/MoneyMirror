namespace MoneyMirror.PhysicalAssets;

/// <summary>
/// A specific product/model inferred for a <see cref="DetectedAsset"/>.
/// Null <see cref="Brand"/>/<see cref="Model"/> (or a low <see cref="Confidence"/>)
/// means the vision model couldn't confidently identify a specific product -
/// callers should treat that as "unknown", never fabricate one.
/// </summary>
public record AssetIdentification(string? Brand, string? Model, double Confidence);

