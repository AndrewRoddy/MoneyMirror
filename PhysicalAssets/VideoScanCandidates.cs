namespace MoneyMirror.PhysicalAssets;

/// <summary>Bounded, in-memory review state for sampled video frames. No inventory writes.</summary>
public sealed class VideoScanCandidates
{
    public const int MaxFrames = 6;
    public const int MaxDetectionsPerFrame = 20;
    public List<VideoCandidate> Items { get; } = [];

    public void AddFrame(int frameIndex, IEnumerable<DetectedAsset> detections)
    {
        if (frameIndex < 0 || frameIndex >= MaxFrames)
        {
            throw new ArgumentOutOfRangeException(nameof(frameIndex));
        }

        var matched = new HashSet<VideoCandidate>();
        foreach (var detection in detections.Take(MaxDetectionsPerFrame))
        {
            if (detection is null || string.IsNullOrWhiteSpace(detection.Label))
            {
                continue;
            }

            var observation = new VideoObservation(frameIndex, detection with
            {
                Region = ValidRegion(detection.Region),
                Tags = detection.Tags ?? [],
                Confidence = double.IsFinite(detection.Confidence) ? Math.Clamp(detection.Confidence, 0, 1) : 0
            });
            // Conservative adjacent-frame association, not physical identity. The UI lets
            // the user split a track when camera motion or identical objects confuse it.
            var candidate = Items.FirstOrDefault(c =>
                !matched.Contains(c)
                && c.Observations[^1].FrameIndex == frameIndex - 1
                && SameDescription(c.Observations[^1].Detection, detection)
                && Overlap(c.Observations[^1].Detection.Region, observation.Detection.Region) >= 0.65);
            if (candidate is null)
            {
                candidate = new VideoCandidate(observation);
                Items.Add(candidate);
            }
            else
            {
                candidate.Observations.Add(observation);
            }

            matched.Add(candidate);
        }
    }

    public void Separate(VideoCandidate candidate, VideoObservation observation)
    {
        if (candidate.Observations.Count < 2 || !candidate.Observations.Remove(observation))
        {
            return;
        }

        if (candidate.Representative == observation)
        {
            candidate.Representative = candidate.Observations[0];
            candidate.StoredImageReference = null;
        }

        Items.Add(new VideoCandidate(observation));
    }

    public static BoundingBox? ValidRegion(BoundingBox? box) =>
        box is not null && double.IsFinite(box.X) && double.IsFinite(box.Y)
        && double.IsFinite(box.Width) && double.IsFinite(box.Height)
        && box.X >= 0 && box.Y >= 0 && box.Width > 0 && box.Height > 0
        && box.X + box.Width <= 1 && box.Y + box.Height <= 1 ? box : null;

    private static bool SameDescription(DetectedAsset a, DetectedAsset b) =>
        string.Equals(a.Label, b.Label, StringComparison.OrdinalIgnoreCase)
        && string.Equals(a.Identification?.Brand, b.Identification?.Brand, StringComparison.OrdinalIgnoreCase)
        && string.Equals(a.Identification?.Model, b.Identification?.Model, StringComparison.OrdinalIgnoreCase);

    private static double Overlap(BoundingBox? a, BoundingBox? b)
    {
        if (a is null || b is null)
        {
            return 0;
        }

        var intersection = Math.Max(0, Math.Min(a.X + a.Width, b.X + b.Width) - Math.Max(a.X, b.X))
            * Math.Max(0, Math.Min(a.Y + a.Height, b.Y + b.Height) - Math.Max(a.Y, b.Y));
        return intersection / (a.Width * a.Height + b.Width * b.Height - intersection);
    }
}

public sealed record VideoObservation(int FrameIndex, DetectedAsset Detection);

public sealed class VideoCandidate(VideoObservation first)
{
    public Guid Id { get; } = Guid.NewGuid();
    public List<VideoObservation> Observations { get; } = [first];
    public VideoObservation Representative { get; set; } = first;
    public bool Selected { get; set; }
    public string? StoredImageReference { get; set; }
}

public sealed record SelectedVideoAsset(DetectedAsset Detection, string ImageReference);
