namespace MoneyMirror.HumanCapital;

/// <summary>
/// One skill named as in-demand by one or more matched occupations, and
/// which of those occupations named it - the grounding for HC7.3.
/// </summary>
public record SkillDemandSignal(string Skill, IReadOnlyList<string> Occupations);

/// <summary>Result of comparing a profile's skills against matched occupations' key skills.</summary>
public record SkillGapAnalysis(
    IReadOnlyList<SkillDemandSignal> MarketableSkills,
    IReadOnlyList<SkillDemandSignal> SkillGaps);

/// <summary>
/// Deterministic comparison of a profile's held skills against the skills
/// matched occupations (<see cref="OccupationMatch.KeySkills"/>) name as in
/// demand. Like <see cref="Financial.FinancialNetWorthCalculator"/>, the AI
/// only supplies input data (which skills an occupation wants) - this
/// comparison itself does not call an LLM.
/// </summary>
public static class SkillGapAnalyzer
{
    public static SkillGapAnalysis Analyze(
        IEnumerable<Skill> profileSkills,
        IEnumerable<OccupationMatch> matchedOccupations)
    {
        ArgumentNullException.ThrowIfNull(profileSkills);
        ArgumentNullException.ThrowIfNull(matchedOccupations);

        var held = profileSkills.Select(s => s.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var occupationsBySkill = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var occupation in matchedOccupations)
        {
            foreach (var skill in occupation.KeySkills)
            {
                if (!occupationsBySkill.TryGetValue(skill, out var occupations))
                {
                    occupations = [];
                    occupationsBySkill[skill] = occupations;
                }

                occupations.Add(occupation.Title);
            }
        }

        var marketable = new List<SkillDemandSignal>();
        var gaps = new List<SkillDemandSignal>();

        foreach (var (skill, occupations) in occupationsBySkill)
        {
            var signal = new SkillDemandSignal(skill, occupations);
            (held.Contains(skill) ? marketable : gaps).Add(signal);
        }

        return new SkillGapAnalysis(marketable, gaps);
    }
}
