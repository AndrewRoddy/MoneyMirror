namespace MoneyMirror.PhysicalAssets;

/// <summary>Fields a user can set when manually adding or editing a physical asset.</summary>
public record PhysicalAssetInput(
    string Name,
    string? Category,
    string? IdentifiedProductModel,
    decimal? InitialValuation);
