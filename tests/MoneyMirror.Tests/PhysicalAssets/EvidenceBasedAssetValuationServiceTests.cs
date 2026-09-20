using MoneyMirror.Ai;
using MoneyMirror.PhysicalAssets;

namespace MoneyMirror.Tests.PhysicalAssets;

public class EvidenceBasedAssetValuationServiceTests
{
    private static readonly DateTimeOffset ValuationDate = new(
        2026,
        9,
        20,
        12,
        0,
        0,
        TimeSpan.Zero
    );

    [Fact]
    public async Task EstimateAsync_UsesMarketListingsAndReturnsStructuredEvidence()
    {
        var source = new FakeMarketDataService([
            new(80m, "Reverb", "Used item A", "Used"),
            new(100m, "Craigslist", "Used item B", "Used"),
            new(140m, "Marketplace", "Used item C", "Used"),
        ]);
        var service = new EvidenceBasedAssetValuationService(
            source,
            UnusedFallback(),
            new FixedTimeProvider(ValuationDate)
        );

        var valuation = await service.EstimateAsync("guitar", "Fender", "CD-60S");

        Assert.Equal(100m, valuation.EstimatedValueUsd);
        Assert.Equal(ValuationDate, valuation.ValuationDate);
        Assert.False(valuation.IsAiEstimated);
        Assert.False(valuation.IsLowConfidence);
        Assert.Equal(3, valuation.Evidence.Count);
        Assert.Equal("guitar", source.LastLabel);
        Assert.Equal("Fender", source.LastBrand);
        Assert.Equal("CD-60S", source.LastModel);
    }

    [Fact]
    public async Task EstimateAsync_NoListingsReturnsExplicitUnavailableLowConfidenceResult()
    {
        var source = new FakeMarketDataService([]);
        var service = new EvidenceBasedAssetValuationService(
            source,
            UnusedFallback(),
            new FixedTimeProvider(ValuationDate)
        );

        var valuation = await service.EstimateAsync("rare item", null, null);

        Assert.Null(valuation.EstimatedValueUsd);
        Assert.Empty(valuation.Evidence);
        Assert.False(valuation.IsAiEstimated);
        Assert.True(valuation.IsLowConfidence);
        Assert.Contains("no usable comparable listings", valuation.Reasoning);
    }

    [Fact]
    public async Task EstimateAsync_ProviderFailureFallsBackToAiEstimate()
    {
        var source = new FakeMarketDataService(
            new EbayMarketDataException("provider unavailable")
        );
        var fallback = new AiEstimatedValuationService(
            new FakeLlmService(
                """{"estimatedValueUsd": 75.00, "reasoning": "A typical used guitar in this condition sells for about $75."}"""
            ),
            new FixedTimeProvider(ValuationDate)
        );
        var service = new EvidenceBasedAssetValuationService(
            source,
            fallback,
            new FixedTimeProvider(ValuationDate)
        );

        var valuation = await service.EstimateAsync("guitar", null, null);

        Assert.Equal(75.00m, valuation.EstimatedValueUsd);
        Assert.True(valuation.IsAiEstimated);
        Assert.Equal("AI estimate (low confidence; not based on live market data)", valuation.SourceLabel);
        Assert.Empty(valuation.Evidence);
    }

    [Fact]
    public async Task EstimateAsync_ProviderFailureAndFallbackFailureThrowsTheFallbacksException()
    {
        var source = new FakeMarketDataService(
            new EbayMarketDataException("provider unavailable")
        );
        var fallback = new AiEstimatedValuationService(
            new ThrowingLlmService(),
            new FixedTimeProvider(ValuationDate)
        );
        var service = new EvidenceBasedAssetValuationService(
            source,
            fallback,
            new FixedTimeProvider(ValuationDate)
        );

        await Assert.ThrowsAsync<AssetValuationException>(() =>
            service.EstimateAsync("guitar", null, null)
        );
    }

    private sealed class FakeMarketDataService(IReadOnlyList<AssetValuationEvidence> result)
        : IMarketDataService
    {
        private readonly Exception? _exception = null;

        public FakeMarketDataService(Exception exception)
            : this([])
        {
            _exception = exception;
        }

        public string? LastLabel { get; private set; }
        public string? LastBrand { get; private set; }
        public string? LastModel { get; private set; }

        public Task<IReadOnlyList<AssetValuationEvidence>> SearchUsedListingsAsync(
            string label,
            string? brand,
            string? model,
            CancellationToken cancellationToken = default
        )
        {
            LastLabel = label;
            LastBrand = brand;
            LastModel = model;
            return _exception is null
                ? Task.FromResult(result)
                : Task.FromException<IReadOnlyList<AssetValuationEvidence>>(_exception);
        }
    }

    private sealed class FakeLlmService(string completion) : ILlmService
    {
        public Task<string> CompleteAsync(string prompt, CancellationToken cancellationToken = default) =>
            Task.FromResult(completion);
    }

    private sealed class ThrowingLlmService : ILlmService
    {
        public Task<string> CompleteAsync(string prompt, CancellationToken cancellationToken = default) =>
            Task.FromException<string>(new LlmServiceException("The LLM is unavailable."));
    }

    // A fallback that must never actually be called - used by tests where the market
    // data provider succeeds, so the fallback path should never execute.
    private static AiEstimatedValuationService UnusedFallback() =>
        new(new ThrowingLlmService());

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
