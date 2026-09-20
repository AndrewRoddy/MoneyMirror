namespace MoneyMirror.HumanCapital;

/// <summary>
/// A candidate occupation matched to a professional profile.
/// </summary>
/// <param name="IsAiEstimated">True when the match is an AI estimate rather than a source-backed occupation.</param>
/// <param name="KeySkills">
/// Skills associated with this occupation by its data provider. Feeds
/// <see cref="SkillGapAnalyzer"/>, which does the profile comparison deterministically.
/// </param>
/// <param name="TypicalMinUsd">
/// A rough typical compensation range for this specific occupation, per
/// the same AI judgment - null when the LLM didn't provide one. This is
/// separate from <see cref="ICompensationEstimationService"/>'s single
/// aggregate range for the whole profile; it exists only to feed
/// <see cref="OpportunityFinder"/>'s per-occupation comparison.
/// </param>
public record OccupationMatch(
    string Title,
    string Explanation,
    bool IsAiEstimated,
    IReadOnlyList<string> KeySkills,
    int? TypicalMinUsd = null,
    int? TypicalMaxUsd = null);
