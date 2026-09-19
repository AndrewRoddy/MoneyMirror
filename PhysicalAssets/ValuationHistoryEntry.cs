namespace MoneyMirror.PhysicalAssets;

/// <summary>One valuation record for an inventory item, including the evidence/explanation behind it.</summary>
public record ValuationHistoryEntry(decimal EstimatedValue, DateTimeOffset ValuedAt, string? Source, string? Notes);
