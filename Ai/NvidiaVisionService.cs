using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using MoneyMirror.Ai.Configuration;

namespace MoneyMirror.Ai;

/// <summary>
/// <see cref="IVisionService"/> implementation calling an NVIDIA multimodal
/// vision model's OpenAI-compatible chat completions endpoint, passing the
/// image as a base64 data URI alongside the instruction prompt.
/// </summary>
public class NvidiaVisionService : IVisionService
{
    private readonly HttpClient _httpClient;
    private readonly VisionModelOptions _options;

    public NvidiaVisionService(HttpClient httpClient, IOptions<VisionModelOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<string> DetectAsync(
        byte[] imageBytes,
        string mediaType,
        string prompt,
        CancellationToken cancellationToken = default
    )
    {
        var dataUri = $"data:{mediaType};base64,{Convert.ToBase64String(imageBytes)}";

        var requestBody = new ChatCompletionRequest
        {
            Model = _options.Model,
            Messages =
            [
                new ChatMessage
                {
                    Role = "user",
                    Content =
                    [
                        new ContentPart { Type = "text", Text = prompt },
                        new ContentPart
                        {
                            Type = "image_url",
                            ImageUrl = new ImageUrl { Url = dataUri },
                        },
                    ],
                },
            ],
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
            throw new VisionServiceException("Failed to reach the vision model provider.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new VisionServiceException("Vision model request timed out.", ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new VisionServiceException(
                    $"Vision model provider returned {(int)response.StatusCode} {response.StatusCode}: {errorBody}"
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
                throw new VisionServiceException("Failed to parse the vision model response.", ex);
            }

            var content = completion?.Choices?.FirstOrDefault()?.Message?.Content;
            if (string.IsNullOrEmpty(content))
            {
                throw new VisionServiceException("Vision model response contained no content.");
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
        public required ContentPart[] Content { get; init; }
    }

    private class ContentPart
    {
        [JsonPropertyName("type")]
        public required string Type { get; init; }

        [JsonPropertyName("text")]
        public string? Text { get; init; }

        [JsonPropertyName("image_url")]
        public ImageUrl? ImageUrl { get; init; }
    }

    private class ImageUrl
    {
        [JsonPropertyName("url")]
        public required string Url { get; init; }
    }

    private class ChatCompletionResponse
    {
        [JsonPropertyName("choices")]
        public ChatChoice[]? Choices { get; init; }
    }

    private class ChatChoice
    {
        [JsonPropertyName("message")]
        public ResponseMessage? Message { get; init; }
    }

    private class ResponseMessage
    {
        [JsonPropertyName("content")]
        public string? Content { get; init; }
    }
}
