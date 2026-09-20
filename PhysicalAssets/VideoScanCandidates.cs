namespace MoneyMirror.PhysicalAssets;

/// <summary>Bounded, in-memory review state for sampled video frames. No inventory writes.</summary>
public sealed class VideoScanCandidates
{
    public const int MaxFrames = 6;
    public const int MaxDetectionsPerFrame = 20;
    public const double MinTrackingOverlap = 0.3;
    public const double MaxCenterDistanceSq = 0.04;
    public const int MinPolygonPoints = 3;
    public const int MaxPolygonPoints = 50;
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

            var validMask = ValidMask(detection.Mask);
            var validRegion = ValidRegion(detection.Region)
                ?? (validMask is not null ? BoundingBoxFromMask(validMask) : null);

            var observation = new VideoObservation(frameIndex, detection with
            {
                Region = validRegion,
                Mask = validMask,
                Tags = detection.Tags ?? [],
                Confidence = double.IsFinite(detection.Confidence) ? Math.Clamp(detection.Confidence, 0, 1) : 0
            });
            // Conservative adjacent-frame association, not physical identity. The UI lets
            // the user split a track when camera motion or identical objects confuse it.
            var candidate = Items.FirstOrDefault(c =>
                !matched.Contains(c)
                && c.Observations[^1].FrameIndex == frameIndex - 1
                && SameDescription(c.Observations[^1].Detection, detection)
                && IsTrackingMatch(c.Observations[^1].Detection.Region, observation.Detection.Region));
            if (candidate is null)
            {
                candidate = new VideoCandidate(observation);
                Items.Add(candidate);
            }
            else
            {
                candidate.Observations.Add(observation);
                if (ScoreObservation(observation) > ScoreObservation(candidate.Representative))
                {
                    candidate.Representative = observation;
                }
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
            candidate.Representative = candidate.Observations
                .OrderByDescending(ScoreObservation)
                .First();
            candidate.StoredImageReference = null;
        }

        Items.Add(new VideoCandidate(observation));
    }

    public static BoundingBox? ValidRegion(BoundingBox? box) =>
        box is not null && double.IsFinite(box.X) && double.IsFinite(box.Y)
        && double.IsFinite(box.Width) && double.IsFinite(box.Height)
        && box.X >= 0 && box.Y >= 0 && box.Width > 0 && box.Height > 0
        && box.X + box.Width <= 1 && box.Y + box.Height <= 1 ? box : null;

    public static IReadOnlyList<NormalizedPoint>? ValidMask(IReadOnlyList<NormalizedPoint>? mask)
    {
        if (mask is null || mask.Count < MinPolygonPoints || mask.Count > MaxPolygonPoints)
        {
            return null;
        }

        var validPoints = new List<NormalizedPoint>(mask.Count);
        foreach (var p in mask)
        {
            if (p is null || !double.IsFinite(p.X) || !double.IsFinite(p.Y))
            {
                return null;
            }

            if (p.X < 0 || p.X > 1 || p.Y < 0 || p.Y > 1)
            {
                return null;
            }

            validPoints.Add(p);
        }

        return validPoints.AsReadOnly();
    }

    public static BoundingBox? BoundingBoxFromMask(IReadOnlyList<NormalizedPoint> mask)
    {
        var minX = mask.Min(p => p.X);
        var maxX = mask.Max(p => p.X);
        var minY = mask.Min(p => p.Y);
        var maxY = mask.Max(p => p.Y);
        var width = maxX - minX;
        var height = maxY - minY;
        if (width <= 0 || height <= 0)
        {
            return null;
        }

        return ValidRegion(new BoundingBox(minX, minY, width, height));
    }

    private static bool SameDescription(DetectedAsset a, DetectedAsset b) =>
        string.Equals(a.Label, b.Label, StringComparison.OrdinalIgnoreCase)
        && string.Equals(a.Identification?.Brand, b.Identification?.Brand, StringComparison.OrdinalIgnoreCase)
        && string.Equals(a.Identification?.Model, b.Identification?.Model, StringComparison.OrdinalIgnoreCase);

    public static bool IsTrackingMatch(BoundingBox? a, BoundingBox? b)
    {
        if (a is null || b is null)
        {
            return false;
        }

        if (Overlap(a, b) >= MinTrackingOverlap)
        {
            return true;
        }

        var intersection = Math.Max(0, Math.Min(a.X + a.Width, b.X + b.Width) - Math.Max(a.X, b.X))
            * Math.Max(0, Math.Min(a.Y + a.Height, b.Y + b.Height) - Math.Max(a.Y, b.Y));
        var minArea = Math.Min(a.Width * a.Height, b.Width * b.Height);
        if (minArea > 0 && (intersection / minArea) >= 0.35)
        {
            return true;
        }

        var dx = (a.X + a.Width / 2.0) - (b.X + b.Width / 2.0);
        var dy = (a.Y + a.Height / 2.0) - (b.Y + b.Height / 2.0);
        return (dx * dx + dy * dy) <= MaxCenterDistanceSq;
    }

    public static double ScoreObservation(VideoObservation obs)
    {
        var confidence = obs.Detection.Confidence;
        if (obs.Detection.Region is not { } box)
        {
            return confidence * 0.5;
        }

        var area = box.Width * box.Height;
        var sizeScore = Math.Clamp(area / 0.15, 0.1, 1.0);
        if (area > 0.8)
        {
            sizeScore *= 0.8;
        }

        var cx = box.X + box.Width / 2.0;
        var cy = box.Y + box.Height / 2.0;
        var distFromCenter = Math.Sqrt(Math.Pow(cx - 0.5, 2) + Math.Pow(cy - 0.5, 2));
        var centralityScore = Math.Max(0.2, 1.0 - distFromCenter);
        var idBonus = obs.Detection.Identification is not null ? 0.2 : 0.0;

        return (confidence * 0.6) + (sizeScore * 0.2) + (centralityScore * 0.2) + idBonus;
    }

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
