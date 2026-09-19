namespace PittMoney.Features.Financial;

/// <summary>
/// Placeholder store until #8 lands EF Core + PostgreSQL. Singleton so all
/// circuits share one list; data resets on app restart.
/// </summary>
public class InMemoryFinancialEntryStore : IFinancialEntryStore
{
    private readonly List<FinancialEntry> _entries = [];

    private readonly Lock _lock = new();

    public event Action? Changed;

    public IReadOnlyList<FinancialEntry> GetAll()
    {
        lock (_lock)
        {
            return _entries.Select(Clone).ToList();
        }
    }

    public void Add(FinancialEntry entry)
    {
        lock (_lock)
        {
            _entries.Add(Clone(entry));
        }
        Changed?.Invoke();
    }

    public void Update(FinancialEntry entry)
    {
        lock (_lock)
        {
            var index = _entries.FindIndex(e => e.Id == entry.Id);
            if (index < 0)
            {
                throw new KeyNotFoundException($"No financial entry with id {entry.Id}.");
            }
            _entries[index] = Clone(entry);
        }
        Changed?.Invoke();
    }

    public void Delete(Guid id)
    {
        bool removed;
        lock (_lock)
        {
            removed = _entries.RemoveAll(e => e.Id == id) > 0;
        }
        if (removed)
        {
            Changed?.Invoke();
        }
    }

    // Hand out copies so callers can't mutate store state behind its back.
    private static FinancialEntry Clone(FinancialEntry e) =>
        new()
        {
            Id = e.Id,
            Type = e.Type,
            Category = e.Category,
            Name = e.Name,
            Amount = e.Amount,
            AsOfDate = e.AsOfDate,
        };
}
