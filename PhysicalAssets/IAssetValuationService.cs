namespace MoneyMirror.PhysicalAssets;

/// <summary>
/// Estimates a physical asset's resale value. The current implementation
/// (#138) is an explicit MVP placeholder that asks the LLM to guess a
/// plausible value - it is not grounded in real market comps. It exists so
/// the app has an end-to-end demo path before #14 (PA5, real market
/// evidence) and #68-71 (PA6, deterministic aggregation of that evidence)
/// are built; see #138 for the swap-out plan.
/// </summary>
public interface IAssetValuationService
{
    /// <returns>A valuation whose EstimatedValueUsd is null when no market value is available.
    /// Callers must preserve that distinction instead of treating it as zero.</returns>
    /// <exception cref="AssetValuationException">
    /// The LLM call failed, or its response couldn't be parsed.
    /// </exception>
    Task<AssetValuation> EstimateAsync(
        string label,
        string? brand,
        string? model,
        CancellationToken cancellationToken = default);
}

