using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using MoneyMirror.PhysicalAssets.Configuration;

namespace MoneyMirror.PhysicalAssets;

/// <summary>Searches eBay's Browse API for used, USD-priced comparable listings.</summary>
public sealed class EbayMarketDataService : IEbayMarketDataService
{
    private const int ResultLimit = 20;
    private const string BrowseScope = "https://api.ebay.com/oauth/api_scope";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly EbayOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private CachedAccessToken? _cachedToken;

    public EbayMarketDataService(
        HttpClient httpClient,
        IOptions<EbayOptions> options,
        TimeProvider? timeProvider = null
    )
    {
        _httpClient = httpClient;
        _options = options.Value;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<IReadOnlyList<AssetValuationEvidence>> SearchUsedListingsAsync(
        string label,
        string? brand,
        string? model,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        EnsureCredentials();

        var query = string.Join(
            " ",
            new[] { label, brand, model }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
        );
        var url = $"{_options.BaseUrl.TrimEnd('/')}/buy/browse/v1/item_summary/search"
            + $"?q={Uri.EscapeDataString(query)}"
            + $"&filter={Uri.EscapeDataString("conditions:{USED}")}"
            + $"&limit={ResultLimit}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await GetAccessTokenAsync(cancellationToken)
        );
        request.Headers.Add("X-EBAY-C-MARKETPLACE-ID", _options.MarketplaceId);

        using var response = await SendAsync(request, "eBay Browse API", cancellationToken);
        var result = await ReadResponseAsync<SearchResponse>(response, "eBay Browse API", cancellationToken);

        return result?.ItemSummaries?
            .Select(ToEvidence)
            .Where(evidence => evidence is not null)
            .Select(evidence => evidence!)
            .ToList() ?? [];
    }

    private void EnsureCredentials()
    {
        if (string.IsNullOrWhiteSpace(_options.ClientId) || string.IsNullOrWhiteSpace(_options.ClientSecret))
        {
            throw new EbayMarketDataException(
                "eBay Browse API credentials are not configured. Set Ebay:ClientId and Ebay:ClientSecret."
            );
        }
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        if (_cachedToken is { ExpiresAt: var expiry } cached && expiry > now.AddMinutes(1))
        {
            return cached.Value;
        }

        await _tokenLock.WaitAsync(cancellationToken);
        try
        {
            now = _timeProvider.GetUtcNow();
            if (_cachedToken is { ExpiresAt: var cachedExpiry } current && cachedExpiry > now.AddMinutes(1))
            {
                return current.Value;
            }

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{_options.BaseUrl.TrimEnd('/')}/identity/v1/oauth2/token"
            );
            var credentials = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{_options.ClientId}:{_options.ClientSecret}")
            );
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            request.Content = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("scope", BrowseScope),
            ]);

            using var response = await SendAsync(request, "eBay OAuth token service", cancellationToken);
            var token = await ReadResponseAsync<TokenResponse>(response, "eBay OAuth token service", cancellationToken);
            if (
                token is null
                || string.IsNullOrWhiteSpace(token.AccessToken)
                || token.ExpiresIn <= 0
            )
            {
                throw new EbayMarketDataException("eBay OAuth token service returned an incomplete access token.");
            }

            _cachedToken = new CachedAccessToken(
                token.AccessToken,
                _timeProvider.GetUtcNow().AddSeconds(token.ExpiresIn)
            );
            return token.AccessToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        string serviceName,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new EbayMarketDataException($"Failed to reach the {serviceName}.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new EbayMarketDataException($"The {serviceName} request timed out.", ex);
        }
    }

    private static async Task<T?> ReadResponseAsync<T>(
        HttpResponseMessage response,
        string serviceName,
        CancellationToken cancellationToken
    )
    {
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new EbayMarketDataException(
                $"{serviceName} returned {(int)response.StatusCode} {response.StatusCode}: {errorBody}"
            );
        }

        try
        {
            return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
        }
        catch (JsonException ex)
        {
            throw new EbayMarketDataException($"Failed to parse the {serviceName} response.", ex);
        }
    }

    private static AssetValuationEvidence? ToEvidence(ItemSummary item)
    {
        if (
            item.Price is null
            || !string.Equals(item.Price.Currency, "USD", StringComparison.OrdinalIgnoreCase)
            || !decimal.TryParse(
                item.Price.Value,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var price
            )
            || price <= 0
        )
        {
            return null;
        }

        return new AssetValuationEvidence(price, "eBay", item.Title, item.Condition);
    }

    private sealed class SearchResponse
    {
        [JsonPropertyName("itemSummaries")]
        public IReadOnlyList<ItemSummary>? ItemSummaries { get; init; }
    }

    private sealed class ItemSummary
    {
        [JsonPropertyName("title")]
        public string? Title { get; init; }

        [JsonPropertyName("condition")]
        public string? Condition { get; init; }

        [JsonPropertyName("price")]
        public ItemPrice? Price { get; init; }
    }

    private sealed class ItemPrice
    {
        [JsonPropertyName("value")]
        public string? Value { get; init; }

        [JsonPropertyName("currency")]
        public string? Currency { get; init; }
    }

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; init; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; init; }
    }

    private sealed record CachedAccessToken(string Value, DateTimeOffset ExpiresAt);
}
