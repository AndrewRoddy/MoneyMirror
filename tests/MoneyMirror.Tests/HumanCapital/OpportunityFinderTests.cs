using MoneyMirror.HumanCapital;

namespace MoneyMirror.Tests.HumanCapital;

public class OpportunityFinderTests
{
    [Fact]
    public void FindOpportunities_HigherPayingOccupationWithFewMissingSkills_IsAnOpportunity()
    {
        var opportunities = OpportunityFinder.FindOpportunities(
            [new Skill("C#", null)],
            [
                new OccupationMatch("Junior Dev", "...", true, ["C#"], TypicalMinUsd: 60000, TypicalMaxUsd: 75000),
                new OccupationMatch("Senior Dev", "...", true, ["C#", "System Design"], TypicalMinUsd: 110000, TypicalMaxUsd: 140000),
            ]);

        var opportunity = Assert.Single(opportunities);
        Assert.Equal("Senior Dev", opportunity.OccupationTitle);
        Assert.Equal(["System Design"], opportunity.MissingSkills);
    }

    [Fact]
    public void FindOpportunities_TooManyMissingSkills_IsExcluded()
    {
        var opportunities = OpportunityFinder.FindOpportunities(
            [new Skill("C#", null)],
            [
                new OccupationMatch("Junior Dev", "...", true, ["C#"], TypicalMinUsd: 60000, TypicalMaxUsd: 75000),
                new OccupationMatch(
                    "Cloud Architect",
                    "...",
                    true,
                    ["Azure", "Kubernetes", "Terraform"],
                    TypicalMinUsd: 150000,
                    TypicalMaxUsd: 190000),
            ],
            maxMissingSkills: 2);

        Assert.Empty(opportunities);
    }

    [Fact]
    public void FindOpportunities_LowestPayingOccupationItself_IsNeverAnOpportunity()
    {
        var opportunities = OpportunityFinder.FindOpportunities(
            [new Skill("C#", null)],
            [new OccupationMatch("Junior Dev", "...", true, ["C#"], TypicalMinUsd: 60000, TypicalMaxUsd: 75000)]);

        Assert.Empty(opportunities);
    }

    [Fact]
    public void FindOpportunities_OccupationWithoutPriceData_IsIgnored()
    {
        var opportunities = OpportunityFinder.FindOpportunities(
            [new Skill("C#", null)],
            [
                new OccupationMatch("Junior Dev", "...", true, ["C#"], TypicalMinUsd: 60000, TypicalMaxUsd: 75000),
                new OccupationMatch("Mystery Role", "...", true, ["C#"]),
            ]);

        Assert.Empty(opportunities);
    }

    [Fact]
    public void FindOpportunities_MultipleOpportunities_OrdersByFewestMissingSkillsFirst()
    {
        var opportunities = OpportunityFinder.FindOpportunities(
            [new Skill("C#", null)],
            [
                new OccupationMatch("Junior Dev", "...", true, ["C#"], TypicalMinUsd: 60000, TypicalMaxUsd: 75000),
                new OccupationMatch("Mid Dev", "...", true, ["C#", "SQL"], TypicalMinUsd: 90000, TypicalMaxUsd: 110000),
                new OccupationMatch(
                    "Senior Dev",
                    "...",
                    true,
                    ["C#"],
                    TypicalMinUsd: 120000,
                    TypicalMaxUsd: 150000),
            ]);

        Assert.Equal(2, opportunities.Count);
        Assert.Equal("Senior Dev", opportunities[0].OccupationTitle);
        Assert.Equal("Mid Dev", opportunities[1].OccupationTitle);
    }

    [Fact]
    public void BuildProgression_OrdersByMidpointPayAscending()
    {
        var progression = OpportunityFinder.BuildProgression(
            [
                new OccupationMatch("Senior Dev", "...", true, [], TypicalMinUsd: 120000, TypicalMaxUsd: 150000),
                new OccupationMatch("Junior Dev", "...", true, [], TypicalMinUsd: 60000, TypicalMaxUsd: 75000),
            ]);

        Assert.Equal(2, progression.Count);
        Assert.Equal("Junior Dev", progression[0].OccupationTitle);
        Assert.Equal("Senior Dev", progression[1].OccupationTitle);
    }

    [Fact]
    public void BuildProgression_OccupationWithoutPriceData_IsExcluded()
    {
        var progression = OpportunityFinder.BuildProgression(
            [
                new OccupationMatch("Junior Dev", "...", true, [], TypicalMinUsd: 60000, TypicalMaxUsd: 75000),
                new OccupationMatch("Mystery Role", "...", true, []),
            ]);

        Assert.Single(progression);
    }

    [Fact]
    public void FindOpportunities_NoOccupationsHavePriceData_ReturnsEmpty()
    {
        var opportunities = OpportunityFinder.FindOpportunities([], [new OccupationMatch("Role", "...", true, [])]);

        Assert.Empty(opportunities);
    }
}
