using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using MoneyMirror.HumanCapital.Configuration;

namespace MoneyMirror.HumanCapital;

/// <summary>Minimal O*NET Web Services client for retrieving occupation data.</summary>
public class OnetOccupationDataService : IOnetOccupationDataService
{
    private readonly HttpClient _httpClient;
    private readonly OnetOptions _options;

    public OnetOccupationDataService(HttpClient httpClient, IOptions<OnetOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<OnetOccupation> GetOccupationAsync(
        string onetSocCode,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(onetSocCode);
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new OnetOccupationDataException("O*NET API key is not configured.");
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{_options.BaseUrl.TrimEnd('/')}/online/occupations/{Uri.EscapeDataString(onetSocCode)}/"
        );
        request.Headers.Add("X-API-Key", _options.ApiKey);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new OnetOccupationDataException("Failed to reach O*NET Web Services.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new OnetOccupationDataException("O*NET Web Services request timed out.", ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new OnetOccupationDataException(
                    $"O*NET Web Services returned {(int)response.StatusCode} {response.StatusCode}: {errorBody}"
                );
            }

            OnetOccupationResponse? result;
            try
            {
                result = await response.Content.ReadFromJsonAsync<OnetOccupationResponse>(cancellationToken);
            }
            catch (Exception ex)
            {
                throw new OnetOccupationDataException("Failed to parse the O*NET Web Services response.", ex);
            }

            if (
                result is null
                || string.IsNullOrWhiteSpace(result.Code)
                || string.IsNullOrWhiteSpace(result.Title)
                || result.Description is null
            )
            {
                throw new OnetOccupationDataException("O*NET Web Services returned an incomplete occupation response.");
            }

            return new OnetOccupation(
                result.Code,
                result.Title,
                result.Description,
                result.SampleReportedTitles ?? []
            );
        }
    }

    private sealed class OnetOccupationResponse
    {
        [JsonPropertyName("code")]
        public string? Code { get; init; }

        [JsonPropertyName("title")]
        public string? Title { get; init; }

        [JsonPropertyName("description")]
        public string? Description { get; init; }

        [JsonPropertyName("sample_of_reported_titles")]
        public IReadOnlyList<string>? SampleReportedTitles { get; init; }
    }
}
