using MoneyMirror.Ai;
using MoneyMirror.HumanCapital;

namespace MoneyMirror.Tests.HumanCapital;

public class AiSuggestedOccupationMatchingServiceTests
{
    private class FakeLlmService(string response) : ILlmService
    {
        public Task<string> CompleteAsync(
            string prompt,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(response);
    }

    private class ThrowingLlmService : ILlmService
    {
        public Task<string> CompleteAsync(
            string prompt,
            CancellationToken cancellationToken = default
        ) => throw new LlmServiceException("provider unavailable");
    }

    private static readonly ProfessionalProfile SampleProfile = ProfessionalProfile.Empty with
    {
        Skills = [new Skill("C#", null), new Skill("SQL", null)],
        Experience = [new Experience("Acme Corp", "Backend Engineer", "2022", null, "Built APIs")],
    };

    private const string ValidJson = """
        {
          "occupations": [
            {"title": "Software Engineer", "explanation": "Strong C# and SQL background from backend engineering experience.", "keySkills": ["C#", "SQL", "Git"], "typicalMinUsd": 85000, "typicalMaxUsd": 130000},
            {"title": "Database Administrator", "explanation": "SQL skills and hands-on backend experience.", "keySkills": ["SQL", "Backup and recovery"]}
          ]
        }
        """;

    [Fact]
    public async Task MatchAsync_ValidJson_ReturnsMatchesMarkedAsAiEstimated()
    {
        var service = new AiSuggestedOccupationMatchingService(new FakeLlmService(ValidJson));

        var matches = await service.MatchAsync(SampleProfile);

        Assert.Equal(2, matches.Count);
        Assert.Equal("Software Engineer", matches[0].Title);
        Assert.All(matches, m => Assert.True(m.IsAiEstimated));
        Assert.Equal(["C#", "SQL", "Git"], matches[0].KeySkills);
        Assert.Equal(85000, matches[0].TypicalMinUsd);
        Assert.Equal(130000, matches[0].TypicalMaxUsd);
    }

    [Fact]
    public async Task MatchAsync_JsonWithoutTypicalCompensation_DefaultsToNull()
    {
        var service = new AiSuggestedOccupationMatchingService(new FakeLlmService(ValidJson));

        var matches = await service.MatchAsync(SampleProfile);

        Assert.Null(matches[1].TypicalMinUsd);
        Assert.Null(matches[1].TypicalMaxUsd);
    }

    [Fact]
    public async Task MatchAsync_JsonWithoutKeySkills_DefaultsToEmptyList()
    {
        const string json = """
            {
              "occupations": [
                {"title": "Software Engineer", "explanation": "Strong C# background."}
              ]
            }
            """;
        var service = new AiSuggestedOccupationMatchingService(new FakeLlmService(json));

        var matches = await service.MatchAsync(SampleProfile);

        Assert.Empty(Assert.Single(matches).KeySkills);
    }

    [Fact]
    public async Task MatchAsync_MissingOccupationsList_ThrowsOccupationMatchingException()
    {
        var service = new AiSuggestedOccupationMatchingService(new FakeLlmService("{}"));

        await Assert.ThrowsAsync<OccupationMatchingException>(() =>
            service.MatchAsync(SampleProfile)
        );
    }

    [Fact]
    public async Task MatchAsync_MatchMissingTitleOrExplanation_ThrowsOccupationMatchingException()
    {
        const string json = "{ \"occupations\": [{ \"title\": \"Software Engineer\" }] }";
        var service = new AiSuggestedOccupationMatchingService(new FakeLlmService(json));

        await Assert.ThrowsAsync<OccupationMatchingException>(() =>
            service.MatchAsync(SampleProfile)
        );
    }

    [Fact]
    public async Task MatchAsync_InvalidCompensationRange_ThrowsOccupationMatchingException()
    {
        const string json = """
            {
              "occupations": [
                {"title": "Software Engineer", "explanation": "Strong C# background.", "typicalMinUsd": 130000, "typicalMaxUsd": 85000}
              ]
            }
            """;
        var service = new AiSuggestedOccupationMatchingService(new FakeLlmService(json));

        await Assert.ThrowsAsync<OccupationMatchingException>(() =>
            service.MatchAsync(SampleProfile)
        );
    }

    [Fact]
    public async Task MatchAsync_ImplausiblySmallCompensationRange_NullsOutBothValuesRatherThanKeepingThem()
    {
        // The model sometimes returns an abbreviated figure (e.g. 80,
        // presumably meaning "$80k") instead of a real annual-salary
        // number. It's a valid, correctly-ordered positive range, so the
        // negative/min>max checks don't catch it - it should still be
        // dropped rather than shown as a nonsensical "$80 - $120".
        const string json = """
            {
              "occupations": [
                {"title": "Software Engineer", "explanation": "Strong C# background.", "typicalMinUsd": 80, "typicalMaxUsd": 120}
              ]
            }
            """;
        var service = new AiSuggestedOccupationMatchingService(new FakeLlmService(json));

        var match = Assert.Single(await service.MatchAsync(SampleProfile));

        Assert.Null(match.TypicalMinUsd);
        Assert.Null(match.TypicalMaxUsd);
    }

    [Fact]
    public async Task MatchAsync_StructuredSkills_AreTrimmedAndDeduplicated()
    {
        const string json = """
            {
              "occupations": [
                {"title": "Software Engineer", "explanation": "Strong C# background.", "keySkills": [" C# ", "c#", "", "SQL"]}
              ]
            }
            """;
        var service = new AiSuggestedOccupationMatchingService(new FakeLlmService(json));

        var match = Assert.Single(await service.MatchAsync(SampleProfile));

        Assert.Equal(["C#", "SQL"], match.KeySkills);
    }

    [Fact]
    public async Task MatchAsync_JsonWrappedInCodeFence_StripsFenceAndParses()
    {
        var fenced = $"```json\n{ValidJson}\n```";
        var service = new AiSuggestedOccupationMatchingService(new FakeLlmService(fenced));

        var matches = await service.MatchAsync(SampleProfile);

        Assert.Equal(2, matches.Count);
    }

    [Fact]
    public async Task MatchAsync_EmptyProfile_ReturnsEmptyWithoutCallingLlm()
    {
        var service = new AiSuggestedOccupationMatchingService(new ThrowingLlmService());

        var matches = await service.MatchAsync(ProfessionalProfile.Empty);

        Assert.Empty(matches);
    }

    [Fact]
    public async Task MatchAsync_MalformedJson_ThrowsOccupationMatchingException()
    {
        var service = new AiSuggestedOccupationMatchingService(new FakeLlmService("not json"));

        await Assert.ThrowsAsync<OccupationMatchingException>(() =>
            service.MatchAsync(SampleProfile)
        );
    }

    [Fact]
    public async Task MatchAsync_LlmServiceFails_ThrowsOccupationMatchingException()
    {
        var service = new AiSuggestedOccupationMatchingService(new ThrowingLlmService());

        await Assert.ThrowsAsync<OccupationMatchingException>(() =>
            service.MatchAsync(SampleProfile)
        );
    }
}
