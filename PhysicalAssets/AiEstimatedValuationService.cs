using System.Text.Json;
using MoneyMirror.Ai;

namespace MoneyMirror.PhysicalAssets;

/// <summary>
/// <see cref="IAssetValuationService"/> implementation that asks the LLM to
/// directly estimate a resale value. MVP placeholder (#138) - not grounded
/// in real market comps. See #138 for the plan to replace this with a
/// pipeline built on real evidence (#14/PA5) and deterministic aggregation
/// (#68-71/PA6).
/// </summary>
public class AiEstimatedValuationService : IAssetValuationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ILlmService _llmService;
    private readonly TimeProvider _timeProvider;

    public AiEstimatedValuationService(ILlmService llmService, TimeProvider? timeProvider = null)
    {
        _llmService = llmService;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<AssetValuation> EstimateAsync(
        string label,
        string? brand,
        string? model,
        CancellationToken cancellationToken = default)
    {
        string completion;
        try
        {
            completion = await _llmService.CompleteAsync(BuildPrompt(label, brand, model), cancellationToken);
        }
        catch (LlmServiceException ex)
        {
            // Carry the provider's own words through: a 503 capacity error and a
            // bug in this app read identically as "the LLM provider call failed".
            throw new AssetValuationException(
                "Failed to estimate a value: the LLM provider call failed.", ex)
            {
                RawResponse = ex.RawResponse,
            };
        }

        var json = StripCodeFence(completion);

        EstimateResponse? estimate;
        try
        {
            estimate = JsonSerializer.Deserialize<EstimateResponse>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new AssetValuationException(
                "The LLM returned a response that could not be parsed as a valuation estimate.", ex)
            {
                RawResponse = completion,
            };
        }

        if (estimate is null
            || estimate.EstimatedValueUsd is not { } value
            || value < 0
            || string.IsNullOrWhiteSpace(estimate.Reasoning))
        {
            throw new AssetValuationException("The LLM returned an unusable valuation estimate.")
            {
                RawResponse = completion,
            };
        }

        return new AssetValuation(
            value,
            estimate.Reasoning,
            _timeProvider.GetUtcNow(),
            IsAiEstimated: true);
    }

    private static string BuildPrompt(string label, string? brand, string? model)
    {
        var item = brand is not null || model is not null
            ? $"{label} ({brand} {model})".Trim()
            : label;

        return $$"""
            Estimate a plausible current used resale value in US dollars for this
            item: {{item}}

            Respond with ONLY a single JSON object - no markdown code fences, no
            commentary - matching exactly this shape:

            {
              "estimatedValueUsd": number,
              "reasoning": string
            }

            "reasoning" should briefly explain the estimate (typical condition,
            age, market factors) in one or two sentences. This is a rough
            estimate from general knowledge, not a lookup of real listings -
            do not claim to have checked specific real listings.
            """;
    }

    private static string StripCodeFence(string text)
    {
        var trimmed = text.Trim();
        if (!trimmed.StartsWith("```"))
        {
            return trimmed;
        }

        var firstNewline = trimmed.IndexOf('\n');
        if (firstNewline >= 0)
        {
            trimmed = trimmed[(firstNewline + 1)..];
        }

        var closingFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
        if (closingFence >= 0)
        {
            trimmed = trimmed[..closingFence];
        }

        return trimmed.Trim();
    }

    private record EstimateResponse(decimal? EstimatedValueUsd, string? Reasoning);
}
