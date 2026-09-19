using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using MoneyMirror.Ai.Configuration;

namespace MoneyMirror.Ai;

/// <summary>
/// <see cref="ILlmService"/> implementation calling NVIDIA Nemotron's
/// OpenAI-compatible chat completions endpoint.
/// </summary>
public class NemotronLlmService : ILlmService
{
    private readonly HttpClient _httpClient;
    private readonly NemotronOptions _options;

    public NemotronLlmService(HttpClient httpClient, IOptions<NemotronOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<string> CompleteAsync(
        string prompt,
        CancellationToken cancellationToken = default
    )
    {
        var requestBody = new ChatCompletionRequest
        {
            Model = _options.Model,
            Messages = [new ChatMessage { Role = "user", Content = prompt }],
        };

        HttpResponseMessage response;
        try
        {
            using var httpRequest = new HttpRequestMessage(
                HttpMethod.Post,
                $"{_options.BaseUrl.TrimEnd('/')}/chat/completions"
            )
            {
                Content = JsonContent.Create(requestBody),
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                _options.ApiKey
            );

            response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new LlmServiceException("Failed to reach the Nemotron LLM provider.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new LlmServiceException("Nemotron LLM request timed out.", ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new LlmServiceException(
                    $"Nemotron LLM provider returned {(int)response.StatusCode} {response.StatusCode}: {errorBody}"
                );
            }

            ChatCompletionResponse? completion;
            try
            {
                completion = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(
                    cancellationToken
                );
            }
            catch (Exception ex)
            {
                throw new LlmServiceException("Failed to parse the Nemotron LLM response.", ex);
            }

            var content = completion?.Choices?.FirstOrDefault()?.Message?.Content;
            if (string.IsNullOrEmpty(content))
            {
                throw new LlmServiceException(
                    "Nemotron LLM response contained no completion content."
                );
            }

            return content;
        }
    }

    private class ChatCompletionRequest
    {
        [JsonPropertyName("model")]
        public required string Model { get; init; }

        [JsonPropertyName("messages")]
        public required ChatMessage[] Messages { get; init; }
    }

    private class ChatMessage
    {
        [JsonPropertyName("role")]
        public required string Role { get; init; }

        [JsonPropertyName("content")]
        public required string Content { get; init; }
    }

    private class ChatCompletionResponse
    {
        [JsonPropertyName("choices")]
        public ChatChoice[]? Choices { get; init; }
    }

    private class ChatChoice
    {
        [JsonPropertyName("message")]
        public ChatMessage? Message { get; init; }
    }
}
