namespace MoneyMirror.PhysicalAssets;

/// <summary>
/// A PA4-confirmed, PA6-valued detection ready to be saved into the
/// inventory (PA7) - the scan-pipeline counterpart to
/// <see cref="PhysicalAssetInput"/>'s manual entry.
/// </summary>
public record ScannedAssetInput(
    string Name,
    string? Category,
    string? IdentifiedProductModel,
    string? ImageReference,
    decimal EstimatedValue,
    string? ValuationEvidence);
