using MoneyMirror.HumanCapital;

namespace MoneyMirror.Tests.HumanCapital;

public class SkillGapAnalyzerTests
{
    [Fact]
    public void Analyze_SkillHeldAndInDemand_IsMarketable()
    {
        var result = SkillGapAnalyzer.Analyze(
            [new Skill("C#", null)],
            [new OccupationMatch("Software Engineer", "...", IsAiEstimated: true, KeySkills: ["C#", "SQL"])]);

        var marketable = Assert.Single(result.MarketableSkills);
        Assert.Equal("C#", marketable.Skill);
        Assert.Equal(["Software Engineer"], marketable.Occupations);
    }

    [Fact]
    public void Analyze_SkillInDemandButNotHeld_IsAGap()
    {
        var result = SkillGapAnalyzer.Analyze(
            [new Skill("C#", null)],
            [new OccupationMatch("Software Engineer", "...", IsAiEstimated: true, KeySkills: ["C#", "SQL"])]);

        var gap = Assert.Single(result.SkillGaps);
        Assert.Equal("SQL", gap.Skill);
    }

    [Fact]
    public void Analyze_SkillMatchIsCaseInsensitive()
    {
        var result = SkillGapAnalyzer.Analyze(
            [new Skill("c#", null)],
            [new OccupationMatch("Software Engineer", "...", IsAiEstimated: true, KeySkills: ["C#"])]);

        Assert.Single(result.MarketableSkills);
        Assert.Empty(result.SkillGaps);
    }

    [Fact]
    public void Analyze_SkillWantedByMultipleOccupations_ListsAllOfThem()
    {
        var result = SkillGapAnalyzer.Analyze(
            [],
            [
                new OccupationMatch("Software Engineer", "...", IsAiEstimated: true, KeySkills: ["SQL"]),
                new OccupationMatch("Database Administrator", "...", IsAiEstimated: true, KeySkills: ["SQL"]),
            ]);

        var gap = Assert.Single(result.SkillGaps);
        Assert.Equal("SQL", gap.Skill);
        Assert.Equal(["Software Engineer", "Database Administrator"], gap.Occupations);
    }

    [Fact]
    public void Analyze_NoOccupations_ReturnsEmptyResult()
    {
        var result = SkillGapAnalyzer.Analyze([new Skill("C#", null)], []);

        Assert.Empty(result.MarketableSkills);
        Assert.Empty(result.SkillGaps);
    }

    [Fact]
    public void Analyze_NoProfileSkills_EverythingIsAGap()
    {
        var result = SkillGapAnalyzer.Analyze(
            [],
            [new OccupationMatch("Software Engineer", "...", IsAiEstimated: true, KeySkills: ["C#", "SQL"])]);

        Assert.Empty(result.MarketableSkills);
        Assert.Equal(2, result.SkillGaps.Count);
    }
}
