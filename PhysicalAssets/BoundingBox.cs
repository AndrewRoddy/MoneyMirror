namespace MoneyMirror.PhysicalAssets;

/// <summary>
/// The region a detected object occupies in its source image, as fractions
/// of image width/height (0-1), so it's resolution-independent.
/// </summary>
public record BoundingBox(double X, double Y, double Width, double Height);

