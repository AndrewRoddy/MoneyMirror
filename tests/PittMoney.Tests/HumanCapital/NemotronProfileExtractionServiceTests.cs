using PittMoney.Ai;
using PittMoney.HumanCapital;

namespace PittMoney.Tests.HumanCapital;

public class NemotronProfileExtractionServiceTests
{
    private class FakeLlmService(string response) : ILlmService
    {
        public Task<string> CompleteAsync(string prompt, CancellationToken cancellationToken = default) =>
            Task.FromResult(response);
    }

    private class ThrowingLlmService : ILlmService
    {
        public Task<string> CompleteAsync(string prompt, CancellationToken cancellationToken = default) =>
            throw new LlmServiceException("provider unavailable");
    }

    private const string ValidJson = """
        {
          "education": [{"institution": "University of Pittsburgh", "degree": "B.S.", "fieldOfStudy": "Computer Science", "startDate": "2018", "endDate": "2022"}],
          "certifications": [],
          "skills": [{"name": "C#", "category": null}],
          "experience": [{"organization": "Acme Corp", "title": "Engineer", "startDate": "2022", "endDate": null, "description": null}],
          "projects": [],
          "publications": [],
          "awards": []
        }
        """;

    [Fact]
    public async Task ExtractAsync_ValidJson_ReturnsPopulatedProfile()
    {
        var service = new NemotronProfileExtractionService(new FakeLlmService(ValidJson));

        var profile = await service.ExtractAsync("some resume text");

        Assert.Single(profile.Education);
        Assert.Equal("University of Pittsburgh", profile.Education[0].Institution);
        Assert.Single(profile.Skills);
        Assert.Single(profile.Experience);
        Assert.Empty(profile.Certifications);
        Assert.Empty(profile.Projects);
    }

    [Fact]
    public async Task ExtractAsync_JsonWrappedInCodeFence_StripsFenceAndParses()
    {
        var fenced = $"```json\n{ValidJson}\n```";
        var service = new NemotronProfileExtractionService(new FakeLlmService(fenced));

        var profile = await service.ExtractAsync("some resume text");

        Assert.Single(profile.Education);
    }

    [Fact]
    public async Task ExtractAsync_BlankResumeText_ReturnsEmptyWithoutCallingLlm()
    {
        var service = new NemotronProfileExtractionService(new ThrowingLlmService());

        var profile = await service.ExtractAsync("   ");

        Assert.Equal(ProfessionalProfile.Empty, profile);
    }

    [Fact]
    public async Task ExtractAsync_MalformedJson_ThrowsProfileExtractionException()
    {
        var service = new NemotronProfileExtractionService(new FakeLlmService("not json at all"));

        await Assert.ThrowsAsync<ProfileExtractionException>(
            () => service.ExtractAsync("some resume text"));
    }

    [Fact]
    public async Task ExtractAsync_LlmServiceFails_ThrowsProfileExtractionException()
    {
        var service = new NemotronProfileExtractionService(new ThrowingLlmService());

        await Assert.ThrowsAsync<ProfileExtractionException>(
            () => service.ExtractAsync("some resume text"));
    }
}
