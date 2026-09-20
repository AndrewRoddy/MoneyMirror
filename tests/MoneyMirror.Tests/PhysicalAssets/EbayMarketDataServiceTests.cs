using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Options;
using MoneyMirror.PhysicalAssets;
using MoneyMirror.PhysicalAssets.Configuration;

namespace MoneyMirror.Tests.PhysicalAssets;

public class EbayMarketDataServiceTests
{
    [Fact]
    public async Task SearchUsedListingsAsync_RequestsUsedUsdListingsAndMapsEvidence()
    {
        var requests = new List<RequestSnapshot>();
        var handler = new CallbackHandler(async (request, cancellationToken) =>
        {
            requests.Add(await RequestSnapshot.CaptureAsync(request, cancellationToken));
            return request.Method == HttpMethod.Post
                ? JsonResponse("""{"access_token":"app-token","expires_in":7200}""")
                : JsonResponse(
                    """{"itemSummaries":[{"title":"Fender CD-60S guitar","condition":"Used","price":{"value":"120.50","currency":"USD"}},{"title":"Foreign listing","condition":"Used","price":{"value":"80","currency":"CAD"}},{"title":"Missing price","condition":"Used"}]}"""
                );
        });
        var service = CreateService(handler);

        var listings = await service.SearchUsedListingsAsync("acoustic guitar", "Fender", "CD-60S");

        var listing = Assert.Single(listings);
        Assert.Equal(120.50m, listing.PriceUsd);
        Assert.Equal("eBay", listing.Source);
        Assert.Equal("Fender CD-60S guitar", listing.ListingTitle);
        Assert.Equal("Used", listing.Condition);

        var tokenRequest = requests[0];
        Assert.Equal(HttpMethod.Post, tokenRequest.Method);
        Assert.EndsWith("/identity/v1/oauth2/token", tokenRequest.Uri.AbsoluteUri);
        Assert.Equal(
            "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("test-client:test-secret")),
            tokenRequest.Authorization
        );
        Assert.Contains("grant_type=client_credentials", tokenRequest.Body);
        Assert.Contains(Uri.EscapeDataString("https://api.ebay.com/oauth/api_scope"), tokenRequest.Body);

        var searchRequest = requests[1];
        Assert.Equal(HttpMethod.Get, searchRequest.Method);
        Assert.Equal("Bearer app-token", searchRequest.Authorization);
        Assert.Equal("EBAY_US", searchRequest.Marketplace);
        Assert.Contains("q=acoustic%20guitar%20Fender%20CD-60S", searchRequest.Uri.Query);
        Assert.Contains("conditions%3A%7BUSED%7D", searchRequest.Uri.Query);
        Assert.Contains("limit=20", searchRequest.Uri.Query);
    }

    [Fact]
    public async Task SearchUsedListingsAsync_EmptyResponseReturnsNoListings()
    {
        var handler = new CallbackHandler((request, _) => Task.FromResult(
            request.Method == HttpMethod.Post
                ? JsonResponse("""{"access_token":"app-token","expires_in":7200}""")
                : JsonResponse("""{"itemSummaries":[]}""")
        ));
        var service = CreateService(handler);

        var listings = await service.SearchUsedListingsAsync("lamp", null, null);

        Assert.Empty(listings);
    }

    [Fact]
    public async Task SearchUsedListingsAsync_MissingCredentialsFailsWithoutCallingProvider()
    {
        var handler = new CallbackHandler((_, _) => throw new InvalidOperationException("Unexpected request."));
        var service = new EbayMarketDataService(
            new HttpClient(handler),
            Options.Create(new EbayOptions())
        );

        var exception = await Assert.ThrowsAsync<EbayMarketDataException>(
            () => service.SearchUsedListingsAsync("lamp", null, null)
        );

        Assert.Contains("credentials are not configured", exception.Message);
    }

    private static EbayMarketDataService CreateService(HttpMessageHandler handler) => new(
        new HttpClient(handler),
        Options.Create(new EbayOptions { ClientId = "test-client", ClientSecret = "test-secret" })
    );

    private static HttpResponseMessage JsonResponse(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
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

    private sealed record RequestSnapshot(
        HttpMethod Method,
        Uri Uri,
        string? Authorization,
        string? Marketplace,
        string Body
    )
    {
        public static async Task<RequestSnapshot> CaptureAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => new(
            request.Method,
            request.RequestUri!,
            request.Headers.Authorization?.ToString(),
            request.Headers.TryGetValues("X-EBAY-C-MARKETPLACE-ID", out var marketplace)
                ? marketplace.Single()
                : null,
            request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken)
        );
    }
}
