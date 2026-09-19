using System.Text.Json;
using MoneyMirror.Ai;

namespace MoneyMirror.HumanCapital;

/// <summary>
/// <see cref="ICompensationEstimationService"/> implementation that asks
/// the LLM to estimate a compensation range directly. MVP placeholder
/// (#148) - not grounded in a real wage-data source. See #148 for the plan
/// to replace this with #24's real pipeline.
/// </summary>
public class AiEstimatedCompensationService : ICompensationEstimationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ILlmService _llmService;

    public AiEstimatedCompensationService(ILlmService llmService)
    {
        _llmService = llmService;
    }

    public async Task<CompensationEstimate> EstimateAsync(
        IReadOnlyList<OccupationMatch> occupations,
        CancellationToken cancellationToken = default)
    {
        if (occupations.Count == 0)
        {
            return new CompensationEstimate(
                0,
                0,
                "No occupation matches to base a compensation estimate on.",
                IsAiEstimated: true);
        }

        string completion;
        try
        {
            completion = await _llmService.CompleteAsync(BuildPrompt(occupations), cancellationToken);
        }
        catch (LlmServiceException ex)
        {
            throw new CompensationEstimationException(
                "Failed to estimate compensation: the LLM provider call failed.", ex);
        }

        var json = StripCodeFence(completion);

        EstimateResponse? estimate;
        try
        {
            estimate = JsonSerializer.Deserialize<EstimateResponse>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new CompensationEstimationException(
                "The LLM returned a response that could not be parsed as a compensation estimate.", ex);
        }

        if (estimate is null || estimate.MinUsd < 0 || estimate.MaxUsd < estimate.MinUsd)
        {
            throw new CompensationEstimationException("The LLM returned an unusable compensation estimate.");
        }

        return new CompensationEstimate(estimate.MinUsd, estimate.MaxUsd, estimate.Explanation, IsAiEstimated: true);
    }

    private static string BuildPrompt(IReadOnlyList<OccupationMatch> occupations)
    {
        var titles = string.Join(", ", occupations.Select(o => o.Title));

        return $$"""
            Estimate a plausible annual compensation range in US dollars for
            someone qualified for these occupations: {{titles}}

            Respond with ONLY a single JSON object - no markdown code fences, no
            commentary - matching exactly this shape:

            {
              "minUsd": number,
              "maxUsd": number,
              "explanation": string
            }

            "explanation" should briefly justify the range (typical seniority
            level implied, market factors) in one or two sentences. This is a
            rough estimate from general knowledge, not a lookup of real wage
            data - do not claim to have checked a real data source.
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

    private record EstimateResponse(decimal MinUsd, decimal MaxUsd, string Explanation);
}
