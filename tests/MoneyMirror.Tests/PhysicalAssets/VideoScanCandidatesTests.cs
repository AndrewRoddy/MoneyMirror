using MoneyMirror.PhysicalAssets;

namespace MoneyMirror.Tests.PhysicalAssets;

public class VideoScanCandidatesTests
{
    private static DetectedAsset Chair(BoundingBox? region = null, string? model = null) =>
        new("Chair", 0.9, region ?? new BoundingBox(0.1, 0.1, 0.3, 0.3),
            model is null ? null : new AssetIdentification("Brand", model, 0.9), []);

    [Fact]
    public void AdjacentSightings_ShareSelection_ButTwoObjectsInOneFrameStaySeparate()
    {
        var candidates = new VideoScanCandidates();
        candidates.AddFrame(0, [Chair(), Chair(new BoundingBox(0.6, 0.1, 0.3, 0.3))]);
        candidates.Items[0].Selected = true;
        candidates.AddFrame(1, [Chair(), Chair(new BoundingBox(0.6, 0.1, 0.3, 0.3))]);
        Assert.Equal(2, candidates.Items.Count);
        Assert.All(candidates.Items, c => Assert.Equal(2, c.Observations.Count));
        Assert.True(candidates.Items[0].Selected);
        Assert.False(candidates.Items[1].Selected);
    }

    [Fact]
    public void MissingBoxOrDifferentModelOrNonAdjacentFrame_DoesNotMerge()
    {
        var candidates = new VideoScanCandidates();
        candidates.AddFrame(0, [Chair(model: "one"), Chair() with { Region = null }]);
        candidates.AddFrame(1, [Chair(model: "two"), Chair() with { Region = null }]);
        candidates.AddFrame(3, [Chair(model: "two")]);
        Assert.Equal(5, candidates.Items.Count);
    }

    [Fact]
    public void UserCanSplitIncorrectAssociation_WithoutLosingOriginalSelection()
    {
        var candidates = new VideoScanCandidates();
        candidates.AddFrame(0, [Chair()]);
        candidates.AddFrame(1, [Chair()]);
        var first = candidates.Items[0];
        first.Selected = true;
        first.Representative = first.Observations[1];
        first.StoredImageReference = "old-crop.jpg";
        candidates.Separate(first, first.Observations[1]);
        Assert.Equal(2, candidates.Items.Count);
        Assert.Single(first.Observations);
        Assert.True(first.Selected);
        Assert.Equal(0, first.Representative.FrameIndex);
        Assert.Null(first.StoredImageReference);
        Assert.False(candidates.Items[1].Selected);
    }

    [Theory]
    [InlineData(-0.1, 0, 0.2, 0.2)]
    [InlineData(0.9, 0, 0.2, 0.2)]
    [InlineData(0, 0, 0, 0.2)]
    [InlineData(double.NaN, 0, 0.2, 0.2)]
    [InlineData(0, 0, double.PositiveInfinity, 0.2)]
    public void InvalidModelCoordinates_FallBackToFrame(double x, double y, double width, double height)
    {
        var candidates = new VideoScanCandidates();
        candidates.AddFrame(0, [Chair(new BoundingBox(x, y, width, height))]);
        Assert.Null(candidates.Items[0].Representative.Detection.Region);
    }

    [Fact]
    public void ScanHasBoundedFrameAndCandidateCounts()
    {
        var candidates = new VideoScanCandidates();
        candidates.AddFrame(0, Enumerable.Repeat(Chair(), 50));
        Assert.Equal(VideoScanCandidates.MaxDetectionsPerFrame, candidates.Items.Count);
        Assert.Throws<ArgumentOutOfRangeException>(() => candidates.AddFrame(6, [Chair()]));
    }

    [Fact]
    public void MalformedModelFields_DoNotBreakReview()
    {
        var candidates = new VideoScanCandidates();
        candidates.AddFrame(0, [null!, Chair() with { Label = " " }, Chair() with { Tags = null!, Confidence = double.NaN }]);
        var detection = Assert.Single(candidates.Items).Representative.Detection;
        Assert.Empty(detection.Tags);
        Assert.Equal(0, detection.Confidence);
    }

    [Fact]
    public void Representative_PicksBestObservation_BasedOnQualityAndConfidence()
    {
        var candidates = new VideoScanCandidates();
        var edgeSighting = new DetectedAsset("Chair", 0.6, new BoundingBox(0.01, 0.01, 0.1, 0.1), null, []);
        var centeredSighting = new DetectedAsset("Chair", 0.95, new BoundingBox(0.05, 0.05, 0.2, 0.2), null, []);
        var lateSighting = new DetectedAsset("Chair", 0.7, new BoundingBox(0.08, 0.08, 0.2, 0.2), null, []);

        candidates.AddFrame(0, [edgeSighting]);
        candidates.AddFrame(1, [centeredSighting]);
        candidates.AddFrame(2, [lateSighting]);

        var candidate = Assert.Single(candidates.Items);
        Assert.Equal(3, candidate.Observations.Count);
        Assert.Equal(1, candidate.Representative.FrameIndex);
        Assert.Equal(0.95, candidate.Representative.Detection.Confidence);
    }

    [Fact]
    public void TrackingAcrossPan_MergesOverlappingSightingsWithModerateIoU()
    {
        var candidates = new VideoScanCandidates();
        var frame0Chair = new DetectedAsset("Chair", 0.9, new BoundingBox(0.1, 0.1, 0.4, 0.4), null, []);
        var frame1Panned = new DetectedAsset("Chair", 0.9, new BoundingBox(0.25, 0.1, 0.4, 0.4), null, []);

        candidates.AddFrame(0, [frame0Chair]);
        candidates.AddFrame(1, [frame1Panned]);

        var candidate = Assert.Single(candidates.Items);
        Assert.Equal(2, candidate.Observations.Count);
    }

    [Fact]
    public void ValidMask_RejectsInvalidCountsAndCoordinates()
    {
        Assert.Null(VideoScanCandidates.ValidMask(null));
        Assert.Null(VideoScanCandidates.ValidMask([new(0.1, 0.1), new(0.2, 0.2)])); // 2 points < 3
        Assert.Null(VideoScanCandidates.ValidMask(Enumerable.Range(0, 51).Select(i => new NormalizedPoint(0.1, 0.1)).ToList()));
        Assert.Null(VideoScanCandidates.ValidMask([new(-0.1, 0.1), new(0.5, 0.5), new(0.2, 0.8)]));
        Assert.Null(VideoScanCandidates.ValidMask([new(double.NaN, 0.1), new(0.5, 0.5), new(0.2, 0.8)]));

        IReadOnlyList<NormalizedPoint> valid = [new(0.1, 0.1), new(0.5, 0.1), new(0.5, 0.5), new(0.1, 0.5)];
        var result = VideoScanCandidates.ValidMask(valid);
        Assert.NotNull(result);
        Assert.Equal(4, result!.Count);
    }

    [Fact]
    public void BoundingBoxFromMask_DerivesRegionWhenRegionIsNull()
    {
        var candidates = new VideoScanCandidates();
        IReadOnlyList<NormalizedPoint> mask = [new(0.1, 0.2), new(0.4, 0.2), new(0.4, 0.6), new(0.1, 0.6)];
        var asset = new DetectedAsset("Chair", 0.9, null, null, [], mask);

        candidates.AddFrame(0, [asset]);

        var candidate = Assert.Single(candidates.Items);
        var region = candidate.Representative.Detection.Region;
        Assert.NotNull(region);
        Assert.Equal(0.1, region!.X, 3);
        Assert.Equal(0.2, region.Y, 3);
        Assert.Equal(0.3, region.Width, 3);
        Assert.Equal(0.4, region.Height, 3);
        Assert.NotNull(candidate.Representative.Detection.Mask);
    }
}
