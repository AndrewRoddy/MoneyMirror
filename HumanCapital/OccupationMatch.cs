namespace MoneyMirror.HumanCapital;

/// <summary>
/// A candidate occupation matched to a professional profile.
/// </summary>
/// <param name="IsAiEstimated">
/// True for this MVP placeholder: the match came from the LLM's judgment,
/// not from a real labor-market data source. Never present this to the
/// user (or persist it) as if it were grounded - #142 is a deliberate,
/// temporary stand-in for the real #23 (ILaborMarketService) pipeline,
/// which sets this false.
/// </param>
/// <param name="KeySkills">
/// Skills typically in demand for this occupation, per the same AI
/// judgment as <paramref name="IsAiEstimated"/> describes. Feeds
/// <see cref="SkillGapAnalyzer"/>, which does the actual profile
/// comparison deterministically - this list is just input data.
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
