using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using MoneyMirror.PhysicalAssets;
using MoneyMirror.PhysicalAssets.Configuration;

namespace MoneyMirror.Tests.PhysicalAssets;

public class EbayMarketDataServiceTests
{
    private const string TokenResponse =
        """{"access_token":"test-access-token","expires_in":7200,"token_type":"Application Access Token"}""";

    [Fact]
    public async Task SearchUsedListingsAsync_AuthenticatesThenSearchesAndMapsOnlyUsedOrRefurbishedUsdResults()
    {
        using var cache = new TemporarySearchCache();
        RequestSnapshot? tokenRequest = null;
        RequestSnapshot? searchRequest = null;
        var handler = new CallbackHandler(async (request, cancellationToken) =>
        {
            if (request.RequestUri!.AbsolutePath.Contains("oauth2/token"))
            {
                tokenRequest = await RequestSnapshot.CaptureAsync(request, cancellationToken);
                return JsonResponse(TokenResponse);
            }

            searchRequest = await RequestSnapshot.CaptureAsync(request, cancellationToken);
            return JsonResponse(
                """
                {"itemSummaries":[
                  {"title":"Used Fender CD-60S Acoustic Guitar","price":{"value":"130.00","currency":"USD"},"conditionId":"3000"},
                  {"title":"New Fender CD-60S Acoustic Guitar","price":{"value":"300.00","currency":"USD"},"conditionId":"1000"},
                  {"title":"Certified Refurbished Fender CD-60S","price":{"value":"145.00","currency":"USD"},"conditionId":"2010"},
                  {"title":"For parts Fender CD-60S","price":{"value":"40.00","currency":"USD"},"conditionId":"7000"},
                  {"title":"Used guitar, foreign listing","price":{"value":"110.00","currency":"GBP"},"conditionId":"3000"}
                ]}
                """
            );
        });
        var service = CreateService(handler, cache.Create(), new EbayTokenCache());

        var listings = await service.SearchUsedListingsAsync("acoustic guitar", "Fender", "CD-60S");

        Assert.Equal(2, listings.Count);
        Assert.Contains(
            listings,
            item => item.PriceUsd == 130m && item.Source == "eBay" && item.Condition == "Used"
        );
        Assert.Contains(listings, item => item.PriceUsd == 145m && item.Condition == "Refurbished");
        Assert.All(listings, item => Assert.NotNull(item.ListingTitle));

        Assert.NotNull(tokenRequest);
        Assert.Equal(HttpMethod.Post, tokenRequest.Method);
        Assert.StartsWith("Basic ", tokenRequest.Authorization);
        Assert.Contains("grant_type=client_credentials", tokenRequest.Body);

        Assert.NotNull(searchRequest);
        Assert.Equal(HttpMethod.Get, searchRequest.Method);
        Assert.Equal("Bearer test-access-token", searchRequest.Authorization);
        Assert.Equal("EBAY_US", searchRequest.MarketplaceId);
        Assert.Contains("q=acoustic%20guitar%20Fender%20CD-60S", searchRequest.Uri.Query);
        Assert.Contains(
            "filter=conditions%3A%7BUSED%7CCERTIFIED_REFURBISHED%7CSELLER_REFURBISHED%7D",
            searchRequest.Uri.Query
        );
    }

    [Fact]
    public async Task SearchUsedListingsAsync_ReusesTheCachedTokenAcrossSearches()
    {
        using var cache = new TemporarySearchCache();
        var tokenRequests = 0;
        var handler = new CallbackHandler((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath.Contains("oauth2/token"))
            {
                tokenRequests++;
                return Task.FromResult(JsonResponse(TokenResponse));
            }

            return Task.FromResult(JsonResponse("""{"itemSummaries":[]}"""));
        });
        var tokenCache = new EbayTokenCache();
        var service = CreateService(handler, cache.Create(), tokenCache);

        await service.SearchUsedListingsAsync("lamp", null, null);
        await service.SearchUsedListingsAsync("chair", null, null);

        Assert.Equal(1, tokenRequests);
    }

    [Fact]
    public async Task SearchUsedListingsAsync_EmptyResponseReturnsNoListings()
    {
        using var cache = new TemporarySearchCache();
        var service = CreateService(
            new CallbackHandler((request, _) =>
                Task.FromResult(
                    JsonResponse(
                        request.RequestUri!.AbsolutePath.Contains("oauth2/token")
                            ? TokenResponse
                            : "{}"
                    )
                )
            ),
            cache.Create(),
            new EbayTokenCache()
        );

        var listings = await service.SearchUsedListingsAsync("lamp", null, null);

        Assert.Empty(listings);
    }

    [Fact]
    public async Task SearchUsedListingsAsync_SearchApiErrorThrows()
    {
        using var cache = new TemporarySearchCache();
        var service = CreateService(
            new CallbackHandler((request, _) =>
                Task.FromResult(
                    request.RequestUri!.AbsolutePath.Contains("oauth2/token")
                        ? JsonResponse(TokenResponse)
                        : new HttpResponseMessage(HttpStatusCode.Unauthorized)
                        {
                            Content = new StringContent(
                                """{"errors":[{"message":"Invalid access token"}]}""",
                                Encoding.UTF8,
                                "application/json"
                            ),
                        }
                )
            ),
            cache.Create(),
            new EbayTokenCache()
        );

        var exception = await Assert.ThrowsAsync<EbayMarketDataException>(() =>
            service.SearchUsedListingsAsync("lamp", null, null)
        );

        Assert.Contains("401", exception.Message);
    }

    [Fact]
    public async Task SearchUsedListingsAsync_TokenRequestFailureThrowsWithoutExposingTheSecret()
    {
        using var cache = new TemporarySearchCache();
        var service = CreateService(
            new CallbackHandler(
                (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized))
            ),
            cache.Create(),
            new EbayTokenCache(),
            new EbayOptions
            {
                ClientId = "test-client-id",
                ClientSecret = "super-secret-value",
            }
        );

        var exception = await Assert.ThrowsAsync<EbayMarketDataException>(() =>
            service.SearchUsedListingsAsync("lamp", null, null)
        );

        Assert.DoesNotContain("super-secret-value", exception.ToString());
    }

    [Fact]
    public async Task SearchUsedListingsAsync_MissingCredentialsFailsWithoutCallingProvider()
    {
        using var cache = new TemporarySearchCache();
        var handler = new CallbackHandler(
            (_, _) => throw new InvalidOperationException("Unexpected request.")
        );
        var service = CreateService(
            handler,
            cache.Create(),
            new EbayTokenCache(),
            new EbayOptions()
        );

        var exception = await Assert.ThrowsAsync<EbayMarketDataException>(() =>
            service.SearchUsedListingsAsync("lamp", null, null)
        );

        Assert.Contains("credentials are not configured", exception.Message);
    }

    [Fact]
    public async Task SearchUsedListingsAsync_ReusesDiskCacheForEquivalentQueriesAcrossServiceInstances()
    {
        using var cache = new TemporarySearchCache();
        var requestCount = 0;
        var firstService = CreateService(
            new CallbackHandler((request, _) =>
            {
                requestCount++;
                return Task.FromResult(
                    JsonResponse(
                        request.RequestUri!.AbsolutePath.Contains("oauth2/token")
                            ? TokenResponse
                            : """{"itemSummaries":[{"title":"Used Fender CD-60S guitar","price":{"value":"130.00","currency":"USD"},"conditionId":"3000"}]}"""
                    )
                );
            }),
            cache.Create(),
            new EbayTokenCache()
        );
        var first = await firstService.SearchUsedListingsAsync("acoustic guitar", "Fender", "CD-60S");

        // A new cache object simulates an app restart while reading the same persistent
        // directory; a fresh token cache too, since it is per-process in production.
        var secondService = CreateService(
            new CallbackHandler(
                (_, _) => throw new InvalidOperationException("A cached search should not call eBay.")
            ),
            cache.Create(),
            new EbayTokenCache()
        );
        var second = await secondService.SearchUsedListingsAsync(
            " ACOUSTIC   GUITAR ",
            "fender",
            "cd-60s"
        );

        Assert.Equal(2, requestCount); // one token fetch + one search, both on the first service
        Assert.Equal(first, second);
    }

    [Fact]
    public async Task SearchUsedListingsAsync_CoalescesConcurrentIdenticalQueries()
    {
        using var cache = new TemporarySearchCache();
        var sharedSearchCache = cache.Create();
        var sharedTokenCache = new EbayTokenCache();
        var searchRequests = 0;
        var requestStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRequest = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstService = CreateService(
            new CallbackHandler(async (request, cancellationToken) =>
            {
                if (request.RequestUri!.AbsolutePath.Contains("oauth2/token"))
                {
                    return JsonResponse(TokenResponse);
                }

                Interlocked.Increment(ref searchRequests);
                requestStarted.SetResult();
                await releaseRequest.Task.WaitAsync(cancellationToken);
                return JsonResponse("""{"itemSummaries":[]}""");
            }),
            sharedSearchCache,
            sharedTokenCache
        );
        var secondService = CreateService(
            new CallbackHandler((request, _) =>
                request.RequestUri!.AbsolutePath.Contains("oauth2/token")
                    ? Task.FromResult(JsonResponse(TokenResponse))
                    : throw new InvalidOperationException("A duplicate search was sent.")
            ),
            sharedSearchCache,
            sharedTokenCache
        );

        var firstTask = firstService.SearchUsedListingsAsync("lamp", null, null);
        await requestStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var secondTask = secondService.SearchUsedListingsAsync("lamp", null, null);
        releaseRequest.SetResult();
        var results = await Task.WhenAll(firstTask, secondTask);

        Assert.Equal(1, searchRequests);
        Assert.Empty(results[0]);
        Assert.Empty(results[1]);
    }

    private static EbayMarketDataService CreateService(
        HttpMessageHandler handler,
        MarketDataSearchCache cache,
        EbayTokenCache tokenCache,
        EbayOptions? options = null
    ) =>
        new(
            new HttpClient(handler),
            Options.Create(
                options
                    ?? new EbayOptions
                    {
                        ClientId = "test-client-id",
                        ClientSecret = "test-client-secret",
                    }
            ),
            cache,
            tokenCache
        );

    private static HttpResponseMessage JsonResponse(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class CallbackHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> callback
    ) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => callback(request, cancellationToken);
    }

    private sealed class TemporarySearchCache : IDisposable
    {
        private readonly string _directory = Path.Combine(
            Path.GetTempPath(),
            $"MoneyMirror-EbaySearchCacheTests-{Guid.NewGuid():N}"
        );

        public MarketDataSearchCache Create() => new(_directory);

        public void Dispose()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
    }

    private sealed record RequestSnapshot(
        HttpMethod Method,
        Uri Uri,
        string? Authorization,
        string? MarketplaceId,
        string Body
    )
    {
        public static async Task<RequestSnapshot> CaptureAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) =>
            new(
                request.Method,
                request.RequestUri!,
                request.Headers.Authorization?.ToString(),
                request.Headers.TryGetValues("X-EBAY-C-MARKETPLACE-ID", out var values)
                    ? values.FirstOrDefault()
                    : null,
                request.Content is null
                    ? string.Empty
                    : await request.Content.ReadAsStringAsync(cancellationToken)
            );
    }
}
