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
}
