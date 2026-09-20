using MoneyMirror.PhysicalAssets;

namespace MoneyMirror.Tests.PhysicalAssets;

public class EvidenceBasedAssetValuationServiceTests
{
    private static readonly DateTimeOffset ValuationDate = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task EstimateAsync_UsesMarketListingsAndReturnsStructuredEvidence()
    {
        var source = new FakeMarketDataService(
        [
            new(80m, "eBay", "Used item A", "Used"),
            new(100m, "eBay", "Used item B", "Very Good"),
            new(140m, "eBay", "Used item C", "Used"),
        ]);
        var service = new EvidenceBasedAssetValuationService(source, new FixedTimeProvider(ValuationDate));

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
        var service = new EvidenceBasedAssetValuationService(source, new FixedTimeProvider(ValuationDate));

        var valuation = await service.EstimateAsync("rare item", null, null);

        Assert.Null(valuation.EstimatedValueUsd);
        Assert.Empty(valuation.Evidence);
        Assert.False(valuation.IsAiEstimated);
        Assert.True(valuation.IsLowConfidence);
        Assert.Contains("no usable comparable listings", valuation.Reasoning);
    }

    [Fact]
    public async Task EstimateAsync_ProviderFailureBecomesDomainException()
    {
        var source = new FakeMarketDataService(new EbayMarketDataException("provider unavailable"));
        var service = new EvidenceBasedAssetValuationService(source, new FixedTimeProvider(ValuationDate));

        var exception = await Assert.ThrowsAsync<AssetValuationException>(
            () => service.EstimateAsync("guitar", null, null)
        );

        Assert.Equal("Failed to retrieve comparable market listings.", exception.Message);
        Assert.IsType<EbayMarketDataException>(exception.InnerException);
    }

    private sealed class FakeMarketDataService(IReadOnlyList<AssetValuationEvidence> result)
        : IEbayMarketDataService
    {
        private readonly Exception? _exception = null;

        public FakeMarketDataService(Exception exception) : this([])
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

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
