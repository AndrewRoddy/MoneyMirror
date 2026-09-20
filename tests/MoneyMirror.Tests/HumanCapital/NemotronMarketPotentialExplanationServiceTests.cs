using MoneyMirror.Ai;
using MoneyMirror.HumanCapital;

namespace MoneyMirror.Tests.HumanCapital;

public class NemotronMarketPotentialExplanationServiceTests
{
    private class FakeLlmService(string response) : ILlmService
    {
        public string? LastPrompt { get; private set; }

        public Task<string> CompleteAsync(string prompt, CancellationToken cancellationToken = default)
        {
            LastPrompt = prompt;
            return Task.FromResult(response);
        }
    }

    private class ThrowingLlmService : ILlmService
    {
        public Task<string> CompleteAsync(string prompt, CancellationToken cancellationToken = default) =>
            throw new LlmServiceException("provider unavailable");
    }

    private static readonly MarketPotentialEstimate SampleEstimate = new(
        "Software Engineer",
        ["OEUS000000000000000000000000001"],
        new BlsCompensationRange(85000m, 130000m, 12),
        2023,
        2023,
        []
    );

    [Fact]
    public async Task ExplainAsync_ValidResponse_ReturnsTrimmedExplanation()
    {
        var service = new NemotronMarketPotentialExplanationService(
            new FakeLlmService("  This range reflects typical software engineer pay.  ")
        );

        var explanation = await service.ExplainAsync(SampleEstimate);

        Assert.Equal("This range reflects typical software engineer pay.", explanation);
    }

    [Fact]
    public async Task ExplainAsync_PromptGroundsInTheGivenRangeAndEvidence()
    {
        var llm = new FakeLlmService("Some explanation.");
        var service = new NemotronMarketPotentialExplanationService(llm);

        await service.ExplainAsync(SampleEstimate);

        Assert.Contains("Software Engineer", llm.LastPrompt);
        Assert.Contains("85000", llm.LastPrompt);
        Assert.Contains("130000", llm.LastPrompt);
        Assert.Contains("OEUS000000000000000000000000001", llm.LastPrompt);
        Assert.Contains("Do not propose a different range", llm.LastPrompt);
    }

    [Fact]
    public async Task ExplainAsync_LlmServiceFails_ThrowsMarketPotentialExplanationException()
    {
        var service = new NemotronMarketPotentialExplanationService(new ThrowingLlmService());

        await Assert.ThrowsAsync<MarketPotentialExplanationException>(() => service.ExplainAsync(SampleEstimate));
    }

    [Fact]
    public async Task ExplainAsync_EmptyResponse_ThrowsMarketPotentialExplanationException()
    {
        var service = new NemotronMarketPotentialExplanationService(new FakeLlmService("   "));

        await Assert.ThrowsAsync<MarketPotentialExplanationException>(() => service.ExplainAsync(SampleEstimate));
    }
}
