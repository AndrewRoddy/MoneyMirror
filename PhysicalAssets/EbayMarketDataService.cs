using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using MoneyMirror.PhysicalAssets.Configuration;

namespace MoneyMirror.PhysicalAssets;

/// <summary>Searches eBay's Browse API for priced used/refurbished listings via a
/// client-credentials app token. Defaults to eBay's sandbox, whose inventory is seeded
/// test data - it answers real requests but rarely has comps for a real product name, and
/// its own condition filter does not reliably exclude new items, so results are always
/// re-checked against <see cref="AssetValuationEvidence"/>'s USD/condition requirements
/// here rather than trusted as returned.</summary>
public sealed class EbayMarketDataService : IMarketDataService
{
    private const int ResultLimit = 50;
    private const string ConditionFilter = "conditions:{USED|CERTIFIED_REFURBISHED|SELLER_REFURBISHED}";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly EbayOptions _options;
    private readonly MarketDataSearchCache _searchCache;
    private readonly EbayTokenCache _tokenCache;

    public EbayMarketDataService(
        HttpClient httpClient,
        IOptions<EbayOptions> options,
        MarketDataSearchCache searchCache,
        EbayTokenCache tokenCache
    )
    {
        _httpClient = httpClient;
        _options = options.Value;
        _searchCache = searchCache;
        _tokenCache = tokenCache;
    }

    public async Task<IReadOnlyList<AssetValuationEvidence>> SearchUsedListingsAsync(
        string label,
        string? brand,
        string? model,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        var query = string.Join(
            " ",
            new[] { label, brand, model }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
        );

        var key = BuildCacheKey(query);
        var cacheLifetime = TimeSpan.FromHours(Math.Clamp(_options.CacheDurationHours, 1, 24 * 365));
        return await _searchCache.GetOrCreateAsync(
            key,
            cacheLifetime,
            () => SearchProviderAsync(query, cancellationToken),
            cancellationToken
        );
    }

    private async Task<IReadOnlyList<AssetValuationEvidence>> SearchProviderAsync(
        string query,
        CancellationToken cancellationToken
    )
    {
        EnsureCredentials();
        var token = await _tokenCache.GetOrCreateAsync(ct => FetchTokenAsync(ct), cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, BuildSearchUri(query));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("X-EBAY-C-MARKETPLACE-ID", _options.MarketplaceId);

        using var response = await SendAsync(request, cancellationToken);
        var result = await ReadResponseAsync<SearchResponse>(
            response,
            "eBay Browse API search",
            cancellationToken
        );

        return MapEvidence(result);
    }

    private async Task<(string Token, TimeSpan ExpiresIn)> FetchTokenAsync(
        CancellationToken cancellationToken
    )
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _options.AuthUrl)
        {
            Content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["scope"] = "https://api.ebay.com/oauth/api_scope",
                }
            ),
        };
        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{_options.ClientId}:{_options.ClientSecret}")
        );
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        using var response = await SendAsync(request, cancellationToken);
        var result = await ReadResponseAsync<TokenResponse>(
            response,
            "eBay OAuth token request",
            cancellationToken
        );

        if (string.IsNullOrWhiteSpace(result.AccessToken))
        {
            throw new EbayMarketDataException("eBay OAuth token response did not include a token.");
        }

        return (result.AccessToken, TimeSpan.FromSeconds(Math.Max(result.ExpiresIn, 60)));
    }

    private string BuildCacheKey(string query)
    {
        var normalizedQuery = System.Text.RegularExpressions.Regex
            .Replace(query.Normalize(NormalizationForm.FormKC), @"\s+", " ")
            .Trim()
            .ToLowerInvariant();
        var keyMaterial = string.Join(
            "\n",
            _options.SearchUrl.Trim(),
            _options.MarketplaceId.Trim().ToUpperInvariant(),
            _options.CacheDurationHours.ToString(CultureInfo.InvariantCulture),
            normalizedQuery
        );
        return Convert
            .ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(keyMaterial)))
            .ToLowerInvariant();
    }

    private void EnsureCredentials()
    {
        if (string.IsNullOrWhiteSpace(_options.ClientId) || string.IsNullOrWhiteSpace(_options.ClientSecret))
        {
            throw new EbayMarketDataException(
                "eBay credentials are not configured. Set Ebay:ClientId and Ebay:ClientSecret."
            );
        }
    }

    private Uri BuildSearchUri(string query)
    {
        var parameters = new Dictionary<string, string>
        {
            ["q"] = query,
            ["filter"] = ConditionFilter,
            ["limit"] = ResultLimit.ToString(CultureInfo.InvariantCulture),
        };
        var queryString = string.Join(
            "&",
            parameters.Select(pair =>
                $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"
            )
        );

        return new Uri($"{_options.SearchUrl.TrimEnd('?', '&')}?{queryString}", UriKind.Absolute);
    }

    private static IReadOnlyList<AssetValuationEvidence> MapEvidence(SearchResponse result)
    {
        var evidence = new List<AssetValuationEvidence>();

        foreach (var item in result.ItemSummaries ?? [])
        {
            var condition = NormalizeCondition(item.ConditionId);
            if (
                condition is null
                || string.IsNullOrWhiteSpace(item.Title)
                || item.Price is not { Currency: "USD" } price
                || !decimal.TryParse(
                    price.Value,
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out var priceUsd
                )
                || priceUsd <= 0
            )
            {
                continue;
            }

            evidence.Add(new AssetValuationEvidence(priceUsd, "eBay", item.Title.Trim(), condition));
        }

        return evidence.Distinct().Take(ResultLimit).ToArray();
    }

    // eBay's documented conditionId taxonomy: 1000-1999 covers New/New-with-defects, 2000-2999
    // is the Refurbished band (certified/seller/excellent/very-good/good), 3000-6999 is Used at
    // every grade (Used, Very Good, Good, Acceptable), and 7000 is "for parts or not working" -
    // not representative of a working item's resale value, so it is excluded like New.
    private static string? NormalizeCondition(string? conditionId)
    {
        if (!int.TryParse(conditionId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
        {
            return null;
        }

        return id switch
        {
            >= 2000 and < 3000 => "Refurbished",
            >= 3000 and < 7000 => "Used",
            _ => null,
        };
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new EbayMarketDataException("Failed to reach eBay.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new EbayMarketDataException("eBay request timed out.", ex);
        }
    }

    private static async Task<T> ReadResponseAsync<T>(
        HttpResponseMessage response,
        string what,
        CancellationToken cancellationToken
    )
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new EbayMarketDataException(
                $"{what} returned {(int)response.StatusCode} {response.StatusCode}."
            );
        }

        try
        {
            var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
            return result ?? throw new EbayMarketDataException($"{what} returned an empty response.");
        }
        catch (JsonException ex)
        {
            throw new EbayMarketDataException($"Failed to parse the {what} response.", ex);
        }
    }

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; init; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; init; }
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

        [JsonPropertyName("price")]
        public Money? Price { get; init; }

        [JsonPropertyName("conditionId")]
        public string? ConditionId { get; init; }
    }

    private sealed class Money
    {
        [JsonPropertyName("value")]
        public string? Value { get; init; }

        [JsonPropertyName("currency")]
        public string? Currency { get; init; }
    }
}
