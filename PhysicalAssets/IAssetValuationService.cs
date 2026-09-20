namespace MoneyMirror.PhysicalAssets;

/// <summary>
/// Estimates a physical asset's resale value from comparable-market evidence.
/// </summary>
public interface IAssetValuationService
{
    /// <returns>A valuation whose EstimatedValueUsd is null when no market value is available.
    /// Callers must preserve that distinction instead of treating it as zero.</returns>
    /// <exception cref="AssetValuationException">
    /// The market-data provider failed or its response could not be parsed.
    /// </exception>
    Task<AssetValuation> EstimateAsync(
        string label,
        string? brand,
        string? model,
        CancellationToken cancellationToken = default);
}

