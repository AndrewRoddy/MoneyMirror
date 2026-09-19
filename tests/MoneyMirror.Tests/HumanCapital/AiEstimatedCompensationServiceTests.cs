using MoneyMirror.Ai;
using MoneyMirror.HumanCapital;

namespace MoneyMirror.Tests.HumanCapital;

public class AiEstimatedCompensationServiceTests
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

    private static readonly IReadOnlyList<OccupationMatch> SampleOccupations =
    [
        new OccupationMatch("Software Engineer", "Strong C# background", IsAiEstimated: true, KeySkills: ["C#", "SQL"]),
    ];

    private const string ValidJson = """
        {
          "minUsd": 85000,
          "maxUsd": 130000,
          "explanation": "Typical range for a mid-level software engineer."
        }
        """;

    [Fact]
    public async Task EstimateAsync_ValidJson_ReturnsRangeMarkedAsAiEstimated()
    {
        var service = new AiEstimatedCompensationService(new FakeLlmService(ValidJson));

        var estimate = await service.EstimateAsync(SampleOccupations);

        Assert.Equal(85000m, estimate.MinUsd);
        Assert.Equal(130000m, estimate.MaxUsd);
        Assert.True(estimate.IsAiEstimated);
    }

    [Fact]
    public async Task EstimateAsync_JsonWrappedInCodeFence_StripsFenceAndParses()
    {
        var fenced = $"```json\n{ValidJson}\n```";
        var service = new AiEstimatedCompensationService(new FakeLlmService(fenced));

        var estimate = await service.EstimateAsync(SampleOccupations);

        Assert.Equal(85000m, estimate.MinUsd);
    }

    [Fact]
    public async Task EstimateAsync_NoOccupations_ReturnsLowConfidenceResultWithoutCallingLlm()
    {
        var service = new AiEstimatedCompensationService(new ThrowingLlmService());

        var estimate = await service.EstimateAsync([]);

        Assert.Equal(0m, estimate.MinUsd);
        Assert.Equal(0m, estimate.MaxUsd);
        Assert.True(estimate.IsAiEstimated);
    }

    [Fact]
    public async Task EstimateAsync_MalformedJson_ThrowsCompensationEstimationException()
    {
        var service = new AiEstimatedCompensationService(new FakeLlmService("not json"));

        await Assert.ThrowsAsync<CompensationEstimationException>(() => service.EstimateAsync(SampleOccupations));
    }

    [Fact]
    public async Task EstimateAsync_MaxBelowMin_ThrowsCompensationEstimationException()
    {
        const string json = """{"minUsd": 100000, "maxUsd": 50000, "explanation": "nonsense"}""";
        var service = new AiEstimatedCompensationService(new FakeLlmService(json));

        await Assert.ThrowsAsync<CompensationEstimationException>(() => service.EstimateAsync(SampleOccupations));
    }

    [Fact]
    public async Task EstimateAsync_LlmServiceFails_ThrowsCompensationEstimationException()
    {
        var service = new AiEstimatedCompensationService(new ThrowingLlmService());

        await Assert.ThrowsAsync<CompensationEstimationException>(() => service.EstimateAsync(SampleOccupations));
    }
}
