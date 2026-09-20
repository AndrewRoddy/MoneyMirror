namespace MoneyMirror.PhysicalAssets;

/// <summary>Caches eBay's client-credentials OAuth token in memory so every search does not
/// spend a round trip re-authenticating. Registered as a singleton: <see cref="EbayMarketDataService"/>
/// is a typed HttpClient, which is transient, so a per-instance cache would never be reused.</summary>
public sealed class EbayTokenCache(TimeProvider? timeProvider = null)
{
    // A token is refreshed a minute before it actually expires, so a request in flight
    // never races an expiring token.
    private static readonly TimeSpan ExpiryMargin = TimeSpan.FromMinutes(1);

    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _token;
    private DateTimeOffset _expiresAtUtc = DateTimeOffset.MinValue;

    public async Task<string> GetOrCreateAsync(
        Func<CancellationToken, Task<(string Token, TimeSpan ExpiresIn)>> factory,
        CancellationToken cancellationToken
    )
    {
        if (_token is { } cached && _timeProvider.GetUtcNow() < _expiresAtUtc)
        {
            return cached;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_token is { } stillCached && _timeProvider.GetUtcNow() < _expiresAtUtc)
            {
                return stillCached;
            }

            var (token, expiresIn) = await factory(cancellationToken);
            _token = token;
            _expiresAtUtc = _timeProvider.GetUtcNow() + expiresIn - ExpiryMargin;
            return token;
        }
        finally
        {
            _lock.Release();
        }
    }
}
