using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using MoneyMirror.HumanCapital;
using MoneyMirror.HumanCapital.Configuration;

namespace MoneyMirror.Tests.HumanCapital;

public class OnetOccupationDataServiceTests
{
    private class FakeHttpMessageHandler(HttpStatusCode statusCode, string content) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            LastRequest = request;
            return Task.FromResult(
                new HttpResponseMessage(statusCode)
                {
                    Content = new StringContent(content, Encoding.UTF8, "application/json"),
                }
            );
        }
    }

    private static OnetOccupationDataService CreateService(FakeHttpMessageHandler handler) =>
        new(
            new HttpClient(handler),
            Options.Create(
                new OnetOptions { BaseUrl = "https://services.onetcenter.org/", ApiKey = "test-key" }
            )
        );

    private const string ValidJson = """
        {
          "code": "15-1252.00",
          "title": "Software Developers",
          "description": "Research, design, and develop computer software.",
          "sample_of_reported_titles": ["Application Developer", "Software Engineer"]
        }
        """;

    [Fact]
    public async Task GetOccupationAsync_ValidResponse_ReturnsRawOccupationData()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, ValidJson);
        var service = CreateService(handler);

        var occupation = await service.GetOccupationAsync("15-1252.00");

        Assert.Equal("15-1252.00", occupation.Code);
        Assert.Equal("Software Developers", occupation.Title);
        Assert.Equal("Research, design, and develop computer software.", occupation.Description);
        Assert.Equal(["Application Developer", "Software Engineer"], occupation.SampleReportedTitles);
        Assert.Equal(HttpMethod.Get, handler.LastRequest!.Method);
        Assert.Equal(
            "https://services.onetcenter.org/online/occupations/15-1252.00/",
            handler.LastRequest.RequestUri!.ToString()
        );
        Assert.Equal("test-key", handler.LastRequest.Headers.GetValues("X-API-Key").Single());
    }

    [Fact]
    public async Task GetOccupationAsync_NoApiKey_ThrowsWithoutCallingApi()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, ValidJson);
        var service = new OnetOccupationDataService(
            new HttpClient(handler),
            Options.Create(new OnetOptions { ApiKey = "" })
        );

        await Assert.ThrowsAsync<OnetOccupationDataException>(
            () => service.GetOccupationAsync("15-1252.00")
        );

        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task GetOccupationAsync_NonSuccessStatusCode_ThrowsOnetOccupationDataException()
    {
        var service = CreateService(
            new FakeHttpMessageHandler(HttpStatusCode.Unauthorized, "invalid API key")
        );

        await Assert.ThrowsAsync<OnetOccupationDataException>(
            () => service.GetOccupationAsync("15-1252.00")
        );
    }

    [Fact]
    public async Task GetOccupationAsync_MalformedJson_ThrowsOnetOccupationDataException()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, "not json"));

        await Assert.ThrowsAsync<OnetOccupationDataException>(
            () => service.GetOccupationAsync("15-1252.00")
        );
    }

    [Fact]
    public async Task GetOccupationAsync_IncompleteResponse_ThrowsOnetOccupationDataException()
    {
        var service = CreateService(
            new FakeHttpMessageHandler(HttpStatusCode.OK, """{ "code": "15-1252.00" }""")
        );

        await Assert.ThrowsAsync<OnetOccupationDataException>(
            () => service.GetOccupationAsync("15-1252.00")
        );
    }
}
