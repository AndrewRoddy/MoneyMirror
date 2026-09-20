namespace MoneyMirror.PhysicalAssets;

/// <summary>
/// CRUD over the physical asset inventory. The landing surface for both
/// entry paths (manual add here, and PA7's AI-confirmed items) - both
/// write to the same table.
/// </summary>
public interface IPhysicalAssetRepository
{
    Task<IReadOnlyList<PhysicalAssetSummary>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <returns>Null if no item with <paramref name="id"/> exists.</returns>
    Task<PhysicalAssetDetail?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Adds a manually-entered item, returning its new Id.</summary>
    Task<Guid> AddAsync(PhysicalAssetInput input, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a PA4-confirmed, PA6-valued detection - the scan-pipeline
    /// counterpart to <see cref="AddAsync"/>. Records the valuation and its
    /// evidence/explanation alongside the new item.
    /// </summary>
    Task<Guid> AddFromScanAsync(ScannedAssetInput input, CancellationToken cancellationToken = default);

    /// <summary>Updates an existing item's fields. Does not touch its valuation history.</summary>
    /// <returns>False if no item with <paramref name="id"/> exists.</returns>
    Task<bool> UpdateAsync(Guid id, PhysicalAssetInput input, CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends a new valuation record for an existing item (e.g. from a PA10
    /// "Revalue" action) without touching its prior valuations - they remain
    /// as history.
    /// </summary>
    /// <returns>False if no item with <paramref name="id"/> exists.</returns>
    Task<bool> AddValuationAsync(
        Guid id,
        decimal value,
        string source,
        string? notes,
        IReadOnlyList<AssetValuationEvidence>? comparableListings = null,
        CancellationToken cancellationToken = default);

    /// <returns>False if no item with <paramref name="id"/> exists.</returns>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds existing inventory items that a new detection/identification
    /// might be a re-scan of (#78): a case-insensitive match on identified
    /// product/model when both have one, otherwise a case-insensitive match
    /// on name. This is a deterministic text match, not visual similarity -
    /// it exists to flag the possibility before saving, not to block it.
    /// </summary>
    Task<IReadOnlyList<PhysicalAssetSummary>> FindPossibleDuplicatesAsync(
        string name,
        string? identifiedProductModel,
        CancellationToken cancellationToken = default);
}
