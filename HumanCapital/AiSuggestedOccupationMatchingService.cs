using System.Text;
using System.Text.Json;
using MoneyMirror.Ai;

namespace MoneyMirror.HumanCapital;

/// <summary>
/// <see cref="IOccupationMatchingService"/> implementation that asks the
/// LLM to suggest plausible occupation matches directly. MVP placeholder
/// (#142) - not grounded in a real labor-market data source. See #142 for
/// the plan to replace this with #23's ILaborMarketService pipeline.
/// </summary>
public class AiSuggestedOccupationMatchingService : IOccupationMatchingService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ILlmService _llmService;

    public AiSuggestedOccupationMatchingService(ILlmService llmService)
    {
        _llmService = llmService;
    }

    public async Task<IReadOnlyList<OccupationMatch>> MatchAsync(
        ProfessionalProfile profile,
        CancellationToken cancellationToken = default)
    {
        if (profile.Skills.Count == 0 && profile.Experience.Count == 0 && profile.Education.Count == 0)
        {
            return [];
        }

        string completion;
        try
        {
            completion = await _llmService.CompleteAsync(BuildPrompt(profile), cancellationToken);
        }
        catch (LlmServiceException ex)
        {
            throw new OccupationMatchingException(
                "Failed to match occupations: the LLM provider call failed.", ex);
        }

        var json = StripCodeFence(completion);

        MatchResponse? response;
        try
        {
            response = JsonSerializer.Deserialize<MatchResponse>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new OccupationMatchingException(
                "The LLM returned a response that could not be parsed as occupation matches.", ex);
        }

        if (response?.Occupations is not { } occupations)
        {
            throw new OccupationMatchingException(
                "The LLM returned an occupation response without a structured occupations list.");
        }

        var matches = new List<OccupationMatch>(occupations.Count);
        foreach (var occupation in occupations)
        {
            if (occupation is null
                || string.IsNullOrWhiteSpace(occupation.Title)
                || string.IsNullOrWhiteSpace(occupation.Explanation))
            {
                throw new OccupationMatchingException(
                    "The LLM returned an occupation match without a title or explanation.");
            }

            if (occupation.TypicalMinUsd is < 0
                || occupation.TypicalMaxUsd is < 0
                || (occupation.TypicalMinUsd is not null
                    && occupation.TypicalMaxUsd is not null
                    && occupation.TypicalMinUsd > occupation.TypicalMaxUsd))
            {
                throw new OccupationMatchingException(
                    "The LLM returned an invalid compensation range for an occupation match.");
            }

            var keySkills = (occupation.KeySkills ?? [])
                .Where(skill => !string.IsNullOrWhiteSpace(skill))
                .Select(skill => skill!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            matches.Add(new OccupationMatch(
                occupation.Title.Trim(),
                occupation.Explanation.Trim(),
                IsAiEstimated: true,
                keySkills,
                occupation.TypicalMinUsd,
                occupation.TypicalMaxUsd));
        }

        return matches;
    }

    private static string BuildPrompt(ProfessionalProfile profile)
    {
        var summary = new StringBuilder();

        if (profile.Skills.Count > 0)
        {
            summary.AppendLine($"Skills: {string.Join(", ", profile.Skills.Select(s => s.Name))}");
        }

        if (profile.Experience.Count > 0)
        {
            summary.AppendLine("Experience:");
            foreach (var exp in profile.Experience)
            {
                var description = exp.Description is not null ? $": {exp.Description}" : string.Empty;
                summary.AppendLine($"- {exp.Title ?? "Role"} at {exp.Organization}{description}");
            }
        }

        if (profile.Education.Count > 0)
        {
            summary.AppendLine("Education:");
            foreach (var edu in profile.Education)
            {
                var field = edu.FieldOfStudy is not null ? $" in {edu.FieldOfStudy}" : string.Empty;
                summary.AppendLine($"- {edu.Degree ?? "Degree"}{field} from {edu.Institution}");
            }
        }

        return $$"""
            Given this professional profile, suggest 3-5 relevant occupations/job
            titles this person is well-suited for, ranked most to least relevant.

            {{summary}}

            Respond with ONLY a single JSON object - no markdown code fences, no
            commentary - matching exactly this shape:

            {
              "occupations": [
                {"title": string, "explanation": string, "keySkills": string[], "typicalMinUsd": number, "typicalMaxUsd": number}
              ]
            }

            "explanation" should briefly name the specific skills/experience from
            the profile above that justify the match (one sentence). Only suggest
            occupations with a clear, explainable connection to the profile.

            "keySkills" should list 3-6 skills typically in demand for that
            occupation (not limited to skills already in the profile above).

            "typicalMinUsd"/"typicalMaxUsd" should be a rough typical US annual
            salary range for that specific occupation (not the whole profile).
            Omit both if you cannot give a reasonable estimate.
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

    private record MatchResponse(IReadOnlyList<OccupationEntry?>? Occupations);

    private record OccupationEntry(
        string? Title,
        string? Explanation,
        IReadOnlyList<string?>? KeySkills,
        int? TypicalMinUsd,
        int? TypicalMaxUsd);
}
