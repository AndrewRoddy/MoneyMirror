using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using MoneyMirror.Ai.Configuration;

namespace MoneyMirror.Ai;

/// <summary>
/// <see cref="ILlmService"/> implementation calling Anthropic Claude's Messages API.
/// Used only as <see cref="FallbackLlmService"/>'s backup - never registered as
/// <see cref="ILlmService"/> on its own.
/// </summary>
public class ClaudeLlmService : ILlmService
{
    private const string ApiKeyHeader = "x-api-key";
    private const string VersionHeader = "anthropic-version";
    private const string WorkspaceIdHeader = "anthropic-workspace-id";

    private readonly HttpClient _httpClient;
    private readonly ClaudeOptions _options;

    public ClaudeLlmService(HttpClient httpClient, IOptions<ClaudeOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<string> CompleteAsync(
        string prompt,
        CancellationToken cancellationToken = default
    )
    {
        var requestBody = new MessagesRequest
        {
            Model = _options.Model,
            Messages = [new ClaudeMessage { Role = "user", Content = prompt }],
            // Mirrors NemotronLlmService's cap: every prompt this service is used for
            // expects a short answer or a compact JSON object.
            MaxTokens = 4096,
        };

        HttpResponseMessage response;
        try
        {
            using var httpRequest = new HttpRequestMessage(
                HttpMethod.Post,
                $"{_options.BaseUrl.TrimEnd('/')}/messages"
            )
            {
                Content = JsonContent.Create(requestBody),
            };
            httpRequest.Headers.Add(ApiKeyHeader, _options.ApiKey);
            httpRequest.Headers.Add(VersionHeader, _options.AnthropicVersion);
            if (!string.IsNullOrEmpty(_options.WorkspaceId))
            {
                httpRequest.Headers.Add(WorkspaceIdHeader, _options.WorkspaceId);
            }

            response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new LlmServiceException("Failed to reach the Claude LLM provider.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new LlmServiceException("Claude LLM request timed out.", ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new LlmServiceException(
                    $"Claude LLM provider returned {(int)response.StatusCode} {response.StatusCode}: {errorBody}"
                )
                {
                    RawResponse = errorBody,
                };
            }

            MessagesResponse? completion;
            try
            {
                completion = await response.Content.ReadFromJsonAsync<MessagesResponse>(
                    cancellationToken
                );
            }
            catch (Exception ex)
            {
                throw new LlmServiceException("Failed to parse the Claude LLM response.", ex);
            }

            var content = completion
                ?.Content?.FirstOrDefault(block => block.Type == "text")
                ?.Text;
            if (string.IsNullOrEmpty(content))
            {
                throw new LlmServiceException(
                    "Claude LLM response contained no completion content."
                )
                {
                    RawResponse = content,
                };
            }

            return content;
        }
    }

    private class MessagesRequest
    {
        [JsonPropertyName("model")]
        public required string Model { get; init; }

        [JsonPropertyName("messages")]
        public required ClaudeMessage[] Messages { get; init; }

        [JsonPropertyName("max_tokens")]
        public required int MaxTokens { get; init; }
    }

    private class ClaudeMessage
    {
        [JsonPropertyName("role")]
        public required string Role { get; init; }

        [JsonPropertyName("content")]
        public required string Content { get; init; }
    }

    private class MessagesResponse
    {
        [JsonPropertyName("content")]
        public ContentBlock[]? Content { get; init; }
    }

    private class ContentBlock
    {
        [JsonPropertyName("type")]
        public string? Type { get; init; }

        [JsonPropertyName("text")]
        public string? Text { get; init; }
    }
}
