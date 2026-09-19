using System.Text.Json;
using PittMoney.Ai;

namespace PittMoney.HumanCapital;

/// <summary>
/// <see cref="IProfessionalProfileExtractionService"/> implementation: prompts
/// Nemotron (via <see cref="ILlmService"/>) for a JSON object matching
/// <see cref="ProfessionalProfile"/>'s shape and parses the response.
/// Structured output is enforced through the prompt plus strict JSON
/// parsing, since <see cref="ILlmService"/> exposes plain prompt-in/text-out.
/// </summary>
public class NemotronProfileExtractionService : IProfessionalProfileExtractionService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ILlmService _llmService;

    public NemotronProfileExtractionService(ILlmService llmService)
    {
        _llmService = llmService;
    }

    public async Task<ProfessionalProfile> ExtractAsync(string resumeText, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(resumeText))
        {
            return ProfessionalProfile.Empty;
        }

        string completion;
        try
        {
            completion = await _llmService.CompleteAsync(BuildPrompt(resumeText), cancellationToken);
        }
        catch (LlmServiceException ex)
        {
            throw new ProfileExtractionException(
                "Failed to extract a professional profile: the LLM provider call failed.", ex);
        }

        var json = StripCodeFence(completion);

        try
        {
            var profile = JsonSerializer.Deserialize<ProfessionalProfile>(json, JsonOptions);
            return profile ?? ProfessionalProfile.Empty;
        }
        catch (JsonException ex)
        {
            throw new ProfileExtractionException(
                "The LLM returned a response that could not be parsed as a structured profile.", ex);
        }
    }

    private static string BuildPrompt(string resumeText) => $$"""
        You extract structured data from resumes. Read the resume text below and
        respond with ONLY a single JSON object - no markdown code fences, no
        commentary - matching exactly this shape:

        {
          "education": [{"institution": string, "degree": string|null, "fieldOfStudy": string|null, "startDate": string|null, "endDate": string|null}],
          "certifications": [{"name": string, "issuingOrganization": string|null, "issueDate": string|null}],
          "skills": [{"name": string, "category": string|null}],
          "experience": [{"organization": string, "title": string|null, "startDate": string|null, "endDate": string|null, "description": string|null}],
          "projects": [{"name": string, "description": string|null}],
          "publications": [{"title": string, "venue": string|null, "date": string|null}],
          "awards": [{"name": string, "issuingOrganization": string|null, "date": string|null}]
        }

        Rules:
        - Only include entries with clear evidence in the resume text below. Never invent entries.
        - If a section has nothing in the resume, return an empty array [] for it - never omit the key.
        - For dates, use the resume's own wording (e.g. "2021", "Jun 2022 - Present"); use null if unknown.

        Resume text:
        ---
        {{resumeText}}
        ---
        """;

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
}
