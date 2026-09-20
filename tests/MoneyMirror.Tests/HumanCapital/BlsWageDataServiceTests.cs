using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using MoneyMirror.HumanCapital;
using MoneyMirror.HumanCapital.Configuration;

namespace MoneyMirror.Tests.HumanCapital;

public class BlsWageDataServiceTests
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
            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json"),
            };
            return Task.FromResult(response);
        }
    }

    private static BlsWageDataService CreateService(FakeHttpMessageHandler handler) =>
        new(
            new HttpClient(handler),
            Options.Create(new BlsOptions { BaseUrl = "https://api.bls.gov/publicAPI/v2/", ApiKey = "test-key" })
        );

    private const string ValidJson = """
        {
          "status": "REQUEST_SUCCEEDED",
          "message": [],
          "Results": {
            "series": [
              {
                "seriesID": "OEUS000000000000000000000000001",
                "data": [
                  {"year": "2023", "period": "A01", "periodName": "Annual", "value": "85000.00", "footnotes": [{"text": ""}]},
                  {"year": "2022", "period": "A01", "periodName": "Annual", "value": "82000.00", "footnotes": [{"text": ""}]}
                ]
              }
            ]
          }
        }
        """;

    [Fact]
    public async Task GetSeriesDataAsync_ValidResponse_ReturnsParsedObservations()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, ValidJson));

        var observations = await service.GetSeriesDataAsync(
            ["OEUS000000000000000000000000001"],
            2022,
            2023
        );

        Assert.Equal(2, observations.Count);
        Assert.Equal("OEUS000000000000000000000000001", observations[0].SeriesId);
        Assert.Equal("2023", observations[0].Year);
        Assert.Equal(85000.00m, observations[0].Value);
        Assert.Equal(82000.00m, observations[1].Value);
    }

    [Fact]
    public async Task GetSeriesDataAsync_SuppressedValue_ParsesAsNullNotZero()
    {
        const string json = """
            {
              "status": "REQUEST_SUCCEEDED",
              "message": [],
              "Results": {
                "series": [
                  {"seriesID": "S1", "data": [{"year": "2023", "period": "A01", "periodName": "Annual", "value": "-", "footnotes": [{"text": "value suppressed"}]}]}
                ]
              }
            }
            """;
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, json));

        var observations = await service.GetSeriesDataAsync(["S1"], 2023, 2023);

        var observation = Assert.Single(observations);
        Assert.Null(observation.Value);
        Assert.Contains("value suppressed", observation.Footnotes);
    }

    [Fact]
    public async Task GetSeriesDataAsync_EmptySeriesIds_ReturnsEmptyWithoutCallingApi()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, ValidJson);
        var service = CreateService(handler);

        var observations = await service.GetSeriesDataAsync([], 2022, 2023);

        Assert.Empty(observations);
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task GetSeriesDataAsync_NonSuccessStatusCode_ThrowsBlsWageDataException()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.InternalServerError, "server error"));

        await Assert.ThrowsAsync<BlsWageDataException>(() => service.GetSeriesDataAsync(["S1"], 2022, 2023));
    }

    [Fact]
    public async Task GetSeriesDataAsync_RequestNotProcessed_ThrowsBlsWageDataExceptionWithMessage()
    {
        const string json = """
            {
              "status": "REQUEST_NOT_PROCESSED",
              "message": ["Series does not exist"],
              "Results": {}
            }
            """;
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, json));

        var ex = await Assert.ThrowsAsync<BlsWageDataException>(
            () => service.GetSeriesDataAsync(["invalid"], 2022, 2023)
        );
        Assert.Contains("Series does not exist", ex.Message);
    }

    [Fact]
    public async Task GetSeriesDataAsync_MalformedJson_ThrowsBlsWageDataException()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, "not json"));

        await Assert.ThrowsAsync<BlsWageDataException>(() => service.GetSeriesDataAsync(["S1"], 2022, 2023));
    }

    [Fact]
    public async Task GetSeriesDataAsync_NoApiKey_OmitsRegistrationKeyRatherThanSendingEmptyString()
    {
        var options = Options.Create(new BlsOptions { BaseUrl = "https://api.bls.gov/publicAPI/v2/", ApiKey = "" });
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, ValidJson);
        var service = new BlsWageDataService(new HttpClient(handler), options);

        await service.GetSeriesDataAsync(["S1"], 2022, 2023);

        var body = await handler.LastRequest!.Content!.ReadAsStringAsync();
        Assert.DoesNotContain("registrationkey", body);
    }
}
