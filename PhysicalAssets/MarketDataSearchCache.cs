using System.Collections.Concurrent;
using System.Text.Json;

namespace MoneyMirror.PhysicalAssets;

/// <summary>Caches successful market-data provider searches on disk so app restarts do not
/// spend the same search again, and coalesces concurrent identical in-flight searches.</summary>
public sealed class MarketDataSearchCache(string cacheDirectory)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ConcurrentDictionary<
        string,
        Lazy<Task<IReadOnlyList<AssetValuationEvidence>>>
    > _inFlight = new();

    public async Task<IReadOnlyList<AssetValuationEvidence>> GetOrCreateAsync(
        string key,
        TimeSpan lifetime,
        Func<Task<IReadOnlyList<AssetValuationEvidence>>> factory,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        var pending = _inFlight.GetOrAdd(
            key,
            _ => new Lazy<Task<IReadOnlyList<AssetValuationEvidence>>>(
                () => LoadOrCreateAsync(key, lifetime, factory),
                LazyThreadSafetyMode.ExecutionAndPublication
            )
        );
        var task = pending.Value;
        _ = task.ContinueWith(
            _ =>
                (
                    (ICollection<
                        KeyValuePair<string, Lazy<Task<IReadOnlyList<AssetValuationEvidence>>>>
                    >)
                        _inFlight
                ).Remove(
                    new KeyValuePair<string, Lazy<Task<IReadOnlyList<AssetValuationEvidence>>>>(
                        key,
                        pending
                    )
                ),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default
        );

        return await task.WaitAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<AssetValuationEvidence>> LoadOrCreateAsync(
        string key,
        TimeSpan lifetime,
        Func<Task<IReadOnlyList<AssetValuationEvidence>>> factory
    )
    {
        try
        {
            Directory.CreateDirectory(cacheDirectory);
        }
        catch (IOException)
        {
            return await factory();
        }
        catch (UnauthorizedAccessException)
        {
            return await factory();
        }

        var cachePath = Path.Combine(cacheDirectory, $"{key}.json");
        var cached = await ReadAsync(cachePath);
        if (
            cached is { Evidence: not null, ExpiresAtUtc: var expiresAt }
            && expiresAt > DateTimeOffset.UtcNow
        )
        {
            return cached.Evidence;
        }

        var evidence = await factory();
        await WriteAsync(cachePath, new CacheEntry(DateTimeOffset.UtcNow.Add(lifetime), evidence));
        return evidence;
    }

    private static async Task<CacheEntry?> ReadAsync(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<CacheEntry>(stream, JsonOptions);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static async Task WriteAsync(string path, CacheEntry entry)
    {
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var stream = File.Create(temporaryPath))
            {
                await JsonSerializer.SerializeAsync(stream, entry, JsonOptions);
            }

            File.Move(temporaryPath, path, overwrite: true);
        }
        catch (IOException)
        {
            // The cache is an optimization; a storage hiccup must not fail a valuation.
        }
        catch (UnauthorizedAccessException)
        {
            // The cache is an optimization; a storage hiccup must not fail a valuation.
        }
        finally
        {
            try
            {
                File.Delete(temporaryPath);
            }
            catch (IOException)
            {
                // Ignore temporary-file cleanup failures.
            }
            catch (UnauthorizedAccessException)
            {
                // Ignore temporary-file cleanup failures.
            }
        }
    }

    private sealed record CacheEntry(
        DateTimeOffset ExpiresAtUtc,
        IReadOnlyList<AssetValuationEvidence> Evidence
    );
}
