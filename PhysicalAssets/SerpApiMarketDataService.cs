using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using MoneyMirror.PhysicalAssets.Configuration;

namespace MoneyMirror.PhysicalAssets;

/// <summary>Searches SerpApi's Google Search results for used, USD-priced listings.</summary>
public sealed class SerpApiMarketDataService : ISerpApiMarketDataService
{
    private const int ResultLimit = 20;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Regex UsdPricePattern = new(
        @"(?<![\p{L}\p{N}])(?:US\s*\$|USD\s*|\$)\s*(?<amount>(?:\d{1,3}(?:,\d{3})+|\d+)(?:\.\d{1,2})?)(?![\d,])",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    );
    private static readonly Regex UsedConditionPattern = new(
        @"\b(?:used|pre[ -]?owned|second[ -]?hand|secondhand)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    );
    private static readonly Regex RefurbishedConditionPattern = new(
        @"\b(?:refurbished|renewed|reconditioned)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    );

    private readonly HttpClient _httpClient;
    private readonly SerpApiOptions _options;

    public SerpApiMarketDataService(HttpClient httpClient, IOptions<SerpApiOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
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

        var query =
            string.Join(
                " ",
                new[] { label, brand, model }
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value!.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
            ) + " used for sale price";

        var uri = BuildSearchUri(query);
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        using var response = await SendAsync(request, cancellationToken);
        var result = await ReadResponseAsync(response, cancellationToken);

        if (!string.IsNullOrWhiteSpace(result.Error))
        {
            throw new SerpApiMarketDataException("SerpApi Google Search returned an API error.");
        }

        return MapEvidence(result);
    }

    private void EnsureCredentials()
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new SerpApiMarketDataException(
                "SerpApi credentials are not configured. Set SerpApi:ApiKey."
            );
        }
    }

    private Uri BuildSearchUri(string query)
    {
        var parameters = new Dictionary<string, string>
        {
            ["engine"] = "google",
            ["q"] = query,
            ["gl"] = _options.CountryCode,
            ["hl"] = _options.Language,
            ["api_key"] = _options.ApiKey,
        };
        var queryString = string.Join(
            "&",
            parameters.Select(pair =>
                $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"
            )
        );

        return new Uri($"{_options.BaseUrl.TrimEnd('?', '&')}?{queryString}", UriKind.Absolute);
    }

    private static IReadOnlyList<AssetValuationEvidence> MapEvidence(SearchResponse result)
    {
        var evidence = new List<AssetValuationEvidence>();

        foreach (var listing in result.ShoppingResults ?? [])
        {
            var condition = NormalizeCondition(listing.SecondHandCondition);
            if (condition is null)
            {
                condition = NormalizeCondition($"{listing.Title} {listing.Snippet}");
            }

            var price = ParseSingleUsdPrice(listing.Price);
            AddEvidence(evidence, listing.Title, listing.Source, listing.Link, condition, price);
        }

        foreach (var listing in result.OrganicResults ?? [])
        {
            var text = $"{listing.Title} {listing.Snippet}";
            var condition = NormalizeCondition(text);
            var price = ParseSingleUsdPrice(text);
            AddEvidence(evidence, listing.Title, listing.Source, listing.Link, condition, price);
        }

        return evidence.Distinct().Take(ResultLimit).ToArray();
    }

    private static void AddEvidence(
        ICollection<AssetValuationEvidence> evidence,
        string? title,
        string? source,
        string? link,
        string? condition,
        decimal? price
    )
    {
        if (
            string.IsNullOrWhiteSpace(title)
            || string.IsNullOrWhiteSpace(condition)
            || price is not > 0
        )
        {
            return;
        }

        evidence.Add(
            new AssetValuationEvidence(
                price.Value,
                ResolveSource(source, link),
                title.Trim(),
                condition
            )
        );
    }

    private static string? NormalizeCondition(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (RefurbishedConditionPattern.IsMatch(text))
        {
            return "Refurbished";
        }

        return UsedConditionPattern.IsMatch(text) ? "Used" : null;
    }

    private static decimal? ParseSingleUsdPrice(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var matches = UsdPricePattern.Matches(text);
        if (matches.Count != 1)
        {
            return null;
        }

        return
            decimal.TryParse(
                matches[0].Groups["amount"].Value,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var price
            )
            && price > 0
            ? price
            : null;
    }

    private static string ResolveSource(string? source, string? link)
    {
        if (!string.IsNullOrWhiteSpace(source))
        {
            return source.Trim();
        }

        return Uri.TryCreate(link, UriKind.Absolute, out var uri)
            ? uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
                ? uri.Host[4..]
                : uri.Host
            : "Google Search";
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
            throw new SerpApiMarketDataException("Failed to reach SerpApi Google Search.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new SerpApiMarketDataException("SerpApi Google Search request timed out.", ex);
        }
    }

    private static async Task<SearchResponse> ReadResponseAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken
    )
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new SerpApiMarketDataException(
                $"SerpApi Google Search returned {(int)response.StatusCode} {response.StatusCode}."
            );
        }

        try
        {
            var result = await response.Content.ReadFromJsonAsync<SearchResponse>(
                JsonOptions,
                cancellationToken
            );
            return result
                ?? throw new SerpApiMarketDataException(
                    "SerpApi Google Search returned an empty response."
                );
        }
        catch (JsonException ex)
        {
            throw new SerpApiMarketDataException(
                "Failed to parse the SerpApi Google Search response.",
                ex
            );
        }
    }

    private sealed class SearchResponse
    {
        [JsonPropertyName("error")]
        public string? Error { get; init; }

        [JsonPropertyName("shopping_results")]
        public IReadOnlyList<SearchListing>? ShoppingResults { get; init; }

        [JsonPropertyName("organic_results")]
        public IReadOnlyList<SearchListing>? OrganicResults { get; init; }
    }

    private sealed class SearchListing
    {
        [JsonPropertyName("title")]
        public string? Title { get; init; }

        [JsonPropertyName("source")]
        public string? Source { get; init; }

        [JsonPropertyName("link")]
        public string? Link { get; init; }

        [JsonPropertyName("snippet")]
        public string? Snippet { get; init; }

        [JsonPropertyName("price")]
        public string? Price { get; init; }

        [JsonPropertyName("second_hand_condition")]
        public string? SecondHandCondition { get; init; }
    }
}
