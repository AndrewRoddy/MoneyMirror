using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using MoneyMirror.PhysicalAssets;
using MoneyMirror.PhysicalAssets.Configuration;

namespace MoneyMirror.Tests.PhysicalAssets;

public class SerpApiMarketDataServiceTests
{
    [Fact]
    public async Task SearchUsedListingsAsync_QueriesGoogleAndMapsOnlyPricedUsedResults()
    {
        using var cache = new TemporarySearchCache();
        RequestSnapshot? requestSnapshot = null;
        var handler = new CallbackHandler(
            async (request, cancellationToken) =>
            {
                requestSnapshot = await RequestSnapshot.CaptureAsync(request, cancellationToken);
                return JsonResponse(
                    """{"shopping_results":[{"title":"Used Fender CD-60S Acoustic Guitar","source":"Reverb","price":"$130.00","second_hand_condition":"Used","link":"https://reverb.com/item/123"},{"title":"New Fender CD-60S Acoustic Guitar","source":"Retailer","price":"$300.00","second_hand_condition":"New","link":"https://store.example/guitar"},{"title":"Refurbished Fender CD-60S","source":"Reseller","price":"$145.00","second_hand_condition":"Refurbished","link":"https://reseller.example/guitar"}],"organic_results":[{"title":"Used Fender acoustic guitar listing","source":"Craigslist","snippet":"Clean condition, asking $110.00.","link":"https://pittsburgh.craigslist.org/item"},{"title":"Vintage guitar price guide","source":"Blog","snippet":"The new model costs $450.00."},{"title":"Used guitar price guide","source":"Blog","snippet":"Examples range from $80.00 to $100.00."}]}"""
                );
            }
        );
        var service = CreateService(handler, cache.Create());

        var listings = await service.SearchUsedListingsAsync("acoustic guitar", "Fender", "CD-60S");

        Assert.Equal(3, listings.Count);
        Assert.Contains(
            listings,
            item => item.PriceUsd == 130m && item.Source == "Reverb" && item.Condition == "Used"
        );
        Assert.Contains(listings, item => item.PriceUsd == 145m && item.Condition == "Refurbished");
        Assert.Contains(listings, item => item.PriceUsd == 110m && item.Source == "Craigslist");
        Assert.All(listings, item => Assert.NotNull(item.ListingTitle));

        Assert.NotNull(requestSnapshot);
        Assert.Equal(HttpMethod.Get, requestSnapshot.Method);
        Assert.Contains("engine=google", requestSnapshot.Uri.Query);
        Assert.Contains(
            "q=acoustic%20guitar%20Fender%20CD-60S%20used%20for%20sale%20price",
            requestSnapshot.Uri.Query
        );
        Assert.Contains("gl=us", requestSnapshot.Uri.Query);
        Assert.Contains("hl=en", requestSnapshot.Uri.Query);
        Assert.Contains("api_key=test-api-key", requestSnapshot.Uri.Query);
        Assert.Null(requestSnapshot.Authorization);
    }

    [Fact]
    public async Task SearchUsedListingsAsync_EmptyResponseReturnsNoListings()
    {
        using var cache = new TemporarySearchCache();
        var requestCount = 0;
        var service = CreateService(
            new CallbackHandler(
                (_, _) =>
                {
                    requestCount++;
                    return Task.FromResult(JsonResponse("{}"));
                }
            ),
            cache.Create()
        );

        var listings = await service.SearchUsedListingsAsync("lamp", null, null);
        var repeatedListings = await service.SearchUsedListingsAsync("lamp", null, null);

        Assert.Empty(listings);
        Assert.Empty(repeatedListings);
        Assert.Equal(1, requestCount);
    }

    [Fact]
    public async Task SearchUsedListingsAsync_ApiErrorThrowsWithoutExposingApiKey()
    {
        using var cache = new TemporarySearchCache();
        var service = CreateService(
            new CallbackHandler(
                (_, _) => Task.FromResult(JsonResponse("""{"error":"Invalid API key"}"""))
            ),
            cache.Create()
        );

        var exception = await Assert.ThrowsAsync<SerpApiMarketDataException>(() =>
            service.SearchUsedListingsAsync("lamp", null, null)
        );

        Assert.DoesNotContain("test-api-key", exception.ToString());
    }

    [Fact]
    public async Task SearchUsedListingsAsync_MissingCredentialsFailsWithoutCallingProvider()
    {
        using var cache = new TemporarySearchCache();
        var handler = new CallbackHandler(
            (_, _) => throw new InvalidOperationException("Unexpected request.")
        );
        var service = CreateService(handler, cache.Create(), new SerpApiOptions());

        var exception = await Assert.ThrowsAsync<SerpApiMarketDataException>(() =>
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
            new CallbackHandler(
                (_, _) =>
                {
                    requestCount++;
                    return Task.FromResult(
                        JsonResponse(
                            """{"shopping_results":[{"title":"Used Fender CD-60S guitar","source":"Reverb","price":"$130.00","second_hand_condition":"Used"}]}"""
                        )
                    );
                }
            ),
            cache.Create()
        );
        var first = await firstService.SearchUsedListingsAsync(
            "acoustic guitar",
            "Fender",
            "CD-60S"
        );

        // A new cache object simulates an app restart while reading the same persistent directory.
        var secondService = CreateService(
            new CallbackHandler(
                (_, _) =>
                    throw new InvalidOperationException("A cached search should not call SerpApi.")
            ),
            cache.Create()
        );
        var second = await secondService.SearchUsedListingsAsync(
            " ACOUSTIC   GUITAR ",
            "fender",
            "cd-60s"
        );

        Assert.Equal(1, requestCount);
        Assert.Equal(first, second);
    }

    [Fact]
    public async Task SearchUsedListingsAsync_CoalescesConcurrentIdenticalQueries()
    {
        using var cache = new TemporarySearchCache();
        var sharedSearchCache = cache.Create();
        var requestCount = 0;
        var requestStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var releaseRequest = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var firstService = CreateService(
            new CallbackHandler(
                async (_, cancellationToken) =>
                {
                    Interlocked.Increment(ref requestCount);
                    requestStarted.SetResult();
                    await releaseRequest.Task.WaitAsync(cancellationToken);
                    return JsonResponse("{}");
                }
            ),
            sharedSearchCache
        );
        var secondService = CreateService(
            new CallbackHandler(
                (_, _) => throw new InvalidOperationException("A duplicate search was sent.")
            ),
            sharedSearchCache
        );

        var firstTask = firstService.SearchUsedListingsAsync("lamp", null, null);
        await requestStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var secondTask = secondService.SearchUsedListingsAsync("lamp", null, null);
        releaseRequest.SetResult();
        var results = await Task.WhenAll(firstTask, secondTask);

        Assert.Equal(1, requestCount);
        Assert.Empty(results[0]);
        Assert.Empty(results[1]);
    }

    private static SerpApiMarketDataService CreateService(
        HttpMessageHandler handler,
        SerpApiSearchCache cache,
        SerpApiOptions? options = null
    ) =>
        new(
            new HttpClient(handler),
            Options.Create(options ?? new SerpApiOptions { ApiKey = "test-api-key" }),
            cache
        );

    private static HttpResponseMessage JsonResponse(string body) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

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
            $"MoneyMirror-SerpApiCacheTests-{Guid.NewGuid():N}"
        );

        public SerpApiSearchCache Create() => new(_directory);

        public void Dispose()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
    }

    private sealed record RequestSnapshot(HttpMethod Method, Uri Uri, string? Authorization)
    {
        public static async Task<RequestSnapshot> CaptureAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            if (request.Content is not null)
            {
                await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return new RequestSnapshot(
                request.Method,
                request.RequestUri!,
                request.Headers.Authorization?.ToString()
            );
        }
    }
}
