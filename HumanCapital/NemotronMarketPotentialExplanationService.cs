using MoneyMirror.Ai;

namespace MoneyMirror.HumanCapital;

/// <summary>
/// <see cref="IMarketPotentialExplanationService"/> implementation that
/// asks Nemotron to explain, in plain prose, a compensation range already
/// computed deterministically from real BLS data (#96/#97/#98). The
/// prompt asks for free text, not a structured number - unlike #148's
/// AI-estimated placeholder, Nemotron has no field to put a number in
/// here, so it has no channel to set or adjust the range, only to
/// describe the one it's given.
/// </summary>
public class NemotronMarketPotentialExplanationService : IMarketPotentialExplanationService
{
    private readonly ILlmService _llmService;

    public NemotronMarketPotentialExplanationService(ILlmService llmService)
    {
        _llmService = llmService;
    }

    public async Task<string> ExplainAsync(
        MarketPotentialEstimate estimate,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(estimate);

        string completion;
        try
        {
            completion = await _llmService.CompleteAsync(BuildPrompt(estimate), cancellationToken);
        }
        catch (LlmServiceException ex)
        {
            throw new MarketPotentialExplanationException(
                "Failed to explain the compensation range: the LLM provider call failed.",
                ex
            );
        }

        var explanation = completion.Trim();
        if (string.IsNullOrEmpty(explanation))
        {
            throw new MarketPotentialExplanationException("The LLM returned an empty explanation.");
        }

        return explanation;
    }

    private static string BuildPrompt(MarketPotentialEstimate estimate)
    {
        var years =
            estimate.StartYear == estimate.EndYear ? estimate.StartYear.ToString() : $"{estimate.StartYear}-{estimate.EndYear}";

        return $$"""
            The following is a real compensation range, already computed from
            official U.S. Bureau of Labor Statistics wage data for
            "{{estimate.Occupation}}" ({{years}}, {{estimate.Range.ObservationCount}}
            wage observations, BLS series {{string.Join(", ", estimate.SeriesIds)}}):

            ${{estimate.Range.MinUsd}} - ${{estimate.Range.MaxUsd}}

            Write a brief (one or two sentence) plain-text explanation of what
            this range represents and why it might vary (e.g. experience
            level, geography, industry). Do not propose a different range or
            any numbers of your own - only explain the range given above
            using the evidence described. Respond with plain text only, no
            JSON, no markdown formatting, no code fences.
            """;
    }
}
