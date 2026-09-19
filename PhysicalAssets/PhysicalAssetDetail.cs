namespace MoneyMirror.PhysicalAssets;

/// <summary>A single inventory item plus its full valuation history (evidence/explanation for each valuation).</summary>
public record PhysicalAssetDetail(
    Guid Id,
    string Name,
    string? Category,
    string? IdentifiedProductModel,
    string? ImageReference,
    bool IsManuallyAdded,
    IReadOnlyList<ValuationHistoryEntry> ValuationHistory);
