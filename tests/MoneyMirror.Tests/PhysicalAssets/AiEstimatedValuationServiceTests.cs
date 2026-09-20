using MoneyMirror.Ai;
using MoneyMirror.PhysicalAssets;

namespace MoneyMirror.Tests.PhysicalAssets;

public class AiEstimatedValuationServiceTests
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

    private static readonly DateTimeOffset FixedNow = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly TimeProvider FixedTimeProvider = new FakeTimeProvider(FixedNow);

    private class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    [Fact]
    public async Task EstimateAsync_ValidJson_ReturnsEstimateMarkedAsAiEstimated()
    {
        const string json = """{"estimatedValueUsd": 150.50, "reasoning": "Typical used price for this model."}""";
        var service = new AiEstimatedValuationService(new FakeLlmService(json), FixedTimeProvider);

        var valuation = await service.EstimateAsync("acoustic guitar", "Fender", "CD-60S");

        Assert.Equal(150.50m, valuation.EstimatedValueUsd);
        Assert.Equal("Typical used price for this model.", valuation.Reasoning);
        Assert.True(valuation.IsAiEstimated);
        Assert.True(valuation.IsLowConfidence);
        Assert.Equal(FixedNow, valuation.ValuationDate);
    }

    [Fact]
    public async Task EstimateAsync_JsonWrappedInCodeFence_StripsFenceAndParses()
    {
        const string json = """{"estimatedValueUsd": 50, "reasoning": "Rough estimate."}""";
        var fenced = $"```json\n{json}\n```";
        var service = new AiEstimatedValuationService(new FakeLlmService(fenced), FixedTimeProvider);

        var valuation = await service.EstimateAsync("lamp", null, null);

        Assert.Equal(50m, valuation.EstimatedValueUsd);
    }

    [Fact]
    public async Task EstimateAsync_MalformedJson_ThrowsAssetValuationException()
    {
        var service = new AiEstimatedValuationService(new FakeLlmService("not json"), FixedTimeProvider);

        await Assert.ThrowsAsync<AssetValuationException>(() => service.EstimateAsync("sofa", null, null));
    }

    [Fact]
    public async Task EstimateAsync_NegativeValue_ThrowsAssetValuationException()
    {
        const string json = """{"estimatedValueUsd": -5, "reasoning": "nonsense"}""";
        var service = new AiEstimatedValuationService(new FakeLlmService(json), FixedTimeProvider);

        await Assert.ThrowsAsync<AssetValuationException>(() => service.EstimateAsync("sofa", null, null));
    }

    [Fact]
    public async Task EstimateAsync_LlmServiceFails_ThrowsAssetValuationException()
    {
        var service = new AiEstimatedValuationService(new ThrowingLlmService(), FixedTimeProvider);

        await Assert.ThrowsAsync<AssetValuationException>(() => service.EstimateAsync("sofa", null, null));
    }
}

