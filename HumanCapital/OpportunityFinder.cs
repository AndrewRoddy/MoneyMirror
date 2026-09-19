namespace MoneyMirror.HumanCapital;

/// <summary>
/// A matched occupation that pays more than the profile's lowest matched
/// occupation and is within reach - the skills it wants beyond what the
/// profile already has are few.
/// </summary>
public record Opportunity(
    string OccupationTitle,
    string Explanation,
    int? TypicalMinUsd,
    int? TypicalMaxUsd,
    IReadOnlyList<string> MissingSkills);

/// <summary>
/// One matched occupation placed in ascending-pay order, alongside the
/// others - an illustrative, AI-estimated progression, not a guaranteed
/// timeline.
/// </summary>
public record ProgressionStep(string OccupationTitle, int MidpointUsd);

/// <summary>
/// Deterministic comparisons over <see cref="OccupationMatch"/> data that
/// already carries <see cref="OccupationMatch.TypicalMinUsd"/> (an AI
/// estimate, like the rest of the MVP occupation-matching placeholder -
/// #142). Like <see cref="SkillGapAnalyzer"/>, the AI only supplies input
/// data; this comparison itself is plain C#, not an LLM call.
/// </summary>
public static class OpportunityFinder
{
    /// <summary>
    /// Occupations that pay more than the lowest-paying matched occupation
    /// and require at most <paramref name="maxMissingSkills"/> skills the
    /// profile doesn't already have, ordered by fewest missing skills first.
    /// Occupations with no compensation estimate are excluded, since "pays
    /// more" can't be evaluated for them.
    /// </summary>
    public static IReadOnlyList<Opportunity> FindOpportunities(
        IEnumerable<Skill> profileSkills,
        IEnumerable<OccupationMatch> matchedOccupations,
        int maxMissingSkills = 2)
    {
        ArgumentNullException.ThrowIfNull(profileSkills);
        ArgumentNullException.ThrowIfNull(matchedOccupations);

        var held = profileSkills.Select(s => s.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var priced = matchedOccupations.Where(o => o.TypicalMinUsd is not null).ToList();

        if (priced.Count == 0)
        {
            return [];
        }

        var baseline = priced.Min(o => o.TypicalMinUsd!.Value);

        return priced
            .Where(o => o.TypicalMinUsd!.Value > baseline)
            .Select(o => new
            {
                Occupation = o,
                Missing = o.KeySkills.Where(skill => !held.Contains(skill)).ToList(),
            })
            .Where(x => x.Missing.Count <= maxMissingSkills)
            .OrderBy(x => x.Missing.Count)
            .ThenByDescending(x => x.Occupation.TypicalMinUsd)
            .Select(x => new Opportunity(
                x.Occupation.Title,
                x.Occupation.Explanation,
                x.Occupation.TypicalMinUsd,
                x.Occupation.TypicalMaxUsd,
                x.Missing))
            .ToList();
    }

    /// <summary>
    /// All priced matched occupations ordered by their estimated midpoint
    /// pay, ascending - an illustrative growth path across the occupations
    /// already matched, not a real career-progression data source.
    /// </summary>
    public static IReadOnlyList<ProgressionStep> BuildProgression(IEnumerable<OccupationMatch> matchedOccupations)
    {
        ArgumentNullException.ThrowIfNull(matchedOccupations);

        return matchedOccupations
            .Where(o => o.TypicalMinUsd is not null && o.TypicalMaxUsd is not null)
            .Select(o => new ProgressionStep(o.Title, (o.TypicalMinUsd!.Value + o.TypicalMaxUsd!.Value) / 2))
            .OrderBy(step => step.MidpointUsd)
            .ToList();
    }
}
