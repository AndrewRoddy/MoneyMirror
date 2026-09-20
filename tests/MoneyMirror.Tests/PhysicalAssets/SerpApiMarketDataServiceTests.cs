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
        var service = CreateService(handler);

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
        var service = CreateService(
            new CallbackHandler((_, _) => Task.FromResult(JsonResponse("{}")))
        );

        var listings = await service.SearchUsedListingsAsync("lamp", null, null);

        Assert.Empty(listings);
    }

    [Fact]
    public async Task SearchUsedListingsAsync_ApiErrorThrowsWithoutExposingApiKey()
    {
        var service = CreateService(
            new CallbackHandler(
                (_, _) => Task.FromResult(JsonResponse("""{"error":"Invalid API key"}"""))
            )
        );

        var exception = await Assert.ThrowsAsync<SerpApiMarketDataException>(() =>
            service.SearchUsedListingsAsync("lamp", null, null)
        );

        Assert.DoesNotContain("test-api-key", exception.ToString());
    }

    [Fact]
    public async Task SearchUsedListingsAsync_MissingCredentialsFailsWithoutCallingProvider()
    {
        var handler = new CallbackHandler(
            (_, _) => throw new InvalidOperationException("Unexpected request.")
        );
        var service = new SerpApiMarketDataService(
            new HttpClient(handler),
            Options.Create(new SerpApiOptions())
        );

        var exception = await Assert.ThrowsAsync<SerpApiMarketDataException>(() =>
            service.SearchUsedListingsAsync("lamp", null, null)
        );

        Assert.Contains("credentials are not configured", exception.Message);
    }

    private static SerpApiMarketDataService CreateService(HttpMessageHandler handler) =>
        new(
            new HttpClient(handler),
            Options.Create(new SerpApiOptions { ApiKey = "test-api-key" })
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
