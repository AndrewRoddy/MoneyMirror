using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using MoneyMirror.HumanCapital.Configuration;

namespace MoneyMirror.HumanCapital;

/// <summary>
/// <see cref="IBlsWageDataService"/> implementation calling the real BLS
/// (Bureau of Labor Statistics) Public Data API v2 time-series endpoint.
/// </summary>
public class BlsWageDataService : IBlsWageDataService
{
    private readonly HttpClient _httpClient;
    private readonly BlsOptions _options;

    public BlsWageDataService(HttpClient httpClient, IOptions<BlsOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<BlsWageObservation>> GetSeriesDataAsync(
        IReadOnlyList<string> seriesIds,
        int startYear,
        int endYear,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(seriesIds);
        if (seriesIds.Count == 0)
        {
            return [];
        }

        var requestBody = new TimeSeriesRequest
        {
            SeriesId = seriesIds.ToArray(),
            StartYear = startYear.ToString(),
            EndYear = endYear.ToString(),
            RegistrationKey = string.IsNullOrWhiteSpace(_options.ApiKey) ? null : _options.ApiKey,
        };

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsJsonAsync(
                $"{_options.BaseUrl.TrimEnd('/')}/timeseries/data/",
                requestBody,
                cancellationToken
            );
        }
        catch (HttpRequestException ex)
        {
            throw new BlsWageDataException("Failed to reach the BLS API.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new BlsWageDataException("BLS API request timed out.", ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new BlsWageDataException(
                    $"BLS API returned {(int)response.StatusCode} {response.StatusCode}: {errorBody}"
                );
            }

            TimeSeriesResponse? result;
            try
            {
                result = await response.Content.ReadFromJsonAsync<TimeSeriesResponse>(cancellationToken);
            }
            catch (Exception ex)
            {
                throw new BlsWageDataException("Failed to parse the BLS API response.", ex);
            }

            if (result is null)
            {
                throw new BlsWageDataException("BLS API returned an empty response.");
            }

            if (!string.Equals(result.Status, "REQUEST_SUCCEEDED", StringComparison.OrdinalIgnoreCase))
            {
                var message = result.Message is { Length: > 0 } ? string.Join(" ", result.Message) : "no message provided";
                throw new BlsWageDataException($"BLS API request was not successful: {message}");
            }

            var series = result.Results?.Series ?? [];
            return series
                .SelectMany(s =>
                    (s.Data ?? []).Select(d => new BlsWageObservation(
                        s.SeriesId ?? string.Empty,
                        d.Year ?? string.Empty,
                        d.Period ?? string.Empty,
                        d.PeriodName ?? string.Empty,
                        ParseValue(d.Value),
                        (d.Footnotes ?? [])
                            .Select(f => f.Text)
                            .Where(t => !string.IsNullOrWhiteSpace(t))
                            .Select(t => t!)
                            .ToList()
                    ))
                )
                .ToList();
        }
    }

    // BLS uses "-" (and similar placeholders) for suppressed/unavailable
    // values rather than omitting the field - never fabricate a number for those.
    private static decimal? ParseValue(string? raw) => decimal.TryParse(raw, out var value) ? value : null;

    private class TimeSeriesRequest
    {
        [JsonPropertyName("seriesid")]
        public required string[] SeriesId { get; init; }

        [JsonPropertyName("startyear")]
        public required string StartYear { get; init; }

        [JsonPropertyName("endyear")]
        public required string EndYear { get; init; }

        [JsonPropertyName("registrationkey")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? RegistrationKey { get; init; }
    }

    private class TimeSeriesResponse
    {
        [JsonPropertyName("status")]
        public string? Status { get; init; }

        [JsonPropertyName("message")]
        public string[]? Message { get; init; }

        [JsonPropertyName("Results")]
        public TimeSeriesResults? Results { get; init; }
    }

    private class TimeSeriesResults
    {
        [JsonPropertyName("series")]
        public TimeSeriesSeries[]? Series { get; init; }
    }

    private class TimeSeriesSeries
    {
        [JsonPropertyName("seriesID")]
        public string? SeriesId { get; init; }

        [JsonPropertyName("data")]
        public TimeSeriesDataPoint[]? Data { get; init; }
    }

    private class TimeSeriesDataPoint
    {
        [JsonPropertyName("year")]
        public string? Year { get; init; }

        [JsonPropertyName("period")]
        public string? Period { get; init; }

        [JsonPropertyName("periodName")]
        public string? PeriodName { get; init; }

        [JsonPropertyName("value")]
        public string? Value { get; init; }

        [JsonPropertyName("footnotes")]
        public TimeSeriesFootnote[]? Footnotes { get; init; }
    }

    private class TimeSeriesFootnote
    {
        [JsonPropertyName("text")]
        public string? Text { get; init; }
    }
}
