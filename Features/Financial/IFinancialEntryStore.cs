namespace PittMoney.Features.Financial;

/// <summary>
/// Storage seam for financial entries. #8 (FC1) replaces the in-memory
/// implementation with EF Core / PostgreSQL; consumers only depend on this.
/// <see cref="Changed"/> fires after any mutation so pages recalculate live.
/// </summary>
public interface IFinancialEntryStore
{
    event Action? Changed;

    IReadOnlyList<FinancialEntry> GetAll();
    void Add(FinancialEntry entry);
    void Update(FinancialEntry entry);
    void Delete(Guid id);
}
