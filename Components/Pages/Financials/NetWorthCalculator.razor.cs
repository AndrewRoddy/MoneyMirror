using Microsoft.AspNetCore.Components;
using MoneyMirror.Features.Financial;

namespace MoneyMirror.Components.Pages.Financials;

/// <summary>
/// Code-behind for <c>NetWorthCalculator.razor</c>. Keeps the page's in-component
/// record lists in sync with <see cref="IFinancialEntryStore"/> so the shared
/// store (and the dashboard) see the same data, without changing the page's
/// markup or logic. Relies on the page's private <c>AssetRecords</c>,
/// <c>LiabilityRecords</c>, and nested <c>FinancialRecord</c> - renaming those
/// in the .razor will surface here as a compile error.
/// </summary>
public partial class NetWorthCalculator : IDisposable
{
    [Inject]
    private IFinancialEntryStore Store { get; set; } = default!;

    // Page record Id -> store entry Id. The page generates its own Ids and
    // exposes them read-only, so we track the mapping here.
    private readonly Dictionary<Guid, Guid> _storeIds = new();

    private bool _pushing;
    private bool _applyingRemoteChange;

    protected override void OnInitialized()
    {
        Store.Changed += OnStoreChanged;

        var existing = Store.GetAll();
        if (existing.Count > 0)
        {
            LoadFromStore(existing);
        }
        // Otherwise the page's seed rows are pushed to the store on first render.
    }

    protected override void OnAfterRender(bool firstRender)
    {
        if (_applyingRemoteChange)
        {
            _applyingRemoteChange = false;
            return;
        }

        PushToStore();
    }

    public void Dispose() => Store.Changed -= OnStoreChanged;

    // Fired by any circuit's mutation. Skip our own writes (see PushToStore).
    private void OnStoreChanged()
    {
        if (_pushing)
        {
            return;
        }

        _ = InvokeAsync(() =>
        {
            _applyingRemoteChange = true;
            LoadFromStore(Store.GetAll());
            StateHasChanged();
        });
    }

    private void LoadFromStore(IReadOnlyList<FinancialEntry> entries)
    {
        AssetRecords.Clear();
        LiabilityRecords.Clear();
        _storeIds.Clear();

        foreach (var entry in entries)
        {
            var kind = entry.Type == FinancialEntryType.Asset ? RecordKind.Asset : RecordKind.Liability;
            var record = new FinancialRecord(kind, entry.Category, entry.Name, entry.Amount, entry.AsOfDate);
            _storeIds[record.Id] = entry.Id;
            (kind == RecordKind.Asset ? AssetRecords : LiabilityRecords).Add(record);
        }
    }

    // Diff page lists against the store; write only what actually changed so a
    // no-op render doesn't fire Changed and loop.
    private void PushToStore()
    {
        _pushing = true;
        try
        {
            var pageRecords = AssetRecords.Concat(LiabilityRecords).ToList();
            var stored = Store.GetAll().ToDictionary(e => e.Id);

            foreach (var record in pageRecords)
            {
                if (_storeIds.TryGetValue(record.Id, out var storeId)
                    && stored.TryGetValue(storeId, out var entry))
                {
                    if (!Matches(entry, record))
                    {
                        Store.Update(ToEntry(record, storeId));
                    }
                }
                else
                {
                    var added = ToEntry(record, Guid.NewGuid());
                    _storeIds[record.Id] = added.Id;
                    Store.Add(added);
                }
            }

            // Records this page knew about that are no longer in its lists were deleted.
            var pageIds = pageRecords.Select(r => r.Id).ToHashSet();
            foreach (var removedPageId in _storeIds.Keys.Where(id => !pageIds.Contains(id)).ToList())
            {
                Store.Delete(_storeIds[removedPageId]);
                _storeIds.Remove(removedPageId);
            }
        }
        finally
        {
            _pushing = false;
        }
    }

    private static bool Matches(FinancialEntry entry, FinancialRecord record) =>
        entry.Type == ToEntryType(record.Kind)
        && entry.Category == record.Type
        && entry.Name == record.Name
        && entry.Amount == record.Amount
        && entry.AsOfDate == record.AsOfDate;

    private static FinancialEntry ToEntry(FinancialRecord record, Guid storeId) =>
        new()
        {
            Id = storeId,
            Type = ToEntryType(record.Kind),
            Category = record.Type,
            Name = record.Name,
            Amount = record.Amount,
            AsOfDate = record.AsOfDate,
        };

    private static FinancialEntryType ToEntryType(RecordKind kind) =>
        kind == RecordKind.Asset ? FinancialEntryType.Asset : FinancialEntryType.Liability;
}
