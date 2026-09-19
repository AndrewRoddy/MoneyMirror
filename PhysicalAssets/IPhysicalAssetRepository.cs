namespace MoneyMirror.PhysicalAssets;

/// <summary>
/// CRUD over the physical asset inventory. The landing surface for both
/// entry paths (manual add here, and PA7's AI-confirmed items) - both
/// write to the same table.
/// </summary>
public interface IPhysicalAssetRepository
{
    Task<IReadOnlyList<PhysicalAssetSummary>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Adds a manually-entered item, returning its new Id.</summary>
    Task<Guid> AddAsync(PhysicalAssetInput input, CancellationToken cancellationToken = default);

    /// <summary>Updates an existing item's fields. Does not touch its valuation history.</summary>
    /// <returns>False if no item with <paramref name="id"/> exists.</returns>
    Task<bool> UpdateAsync(Guid id, PhysicalAssetInput input, CancellationToken cancellationToken = default);

    /// <returns>False if no item with <paramref name="id"/> exists.</returns>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
