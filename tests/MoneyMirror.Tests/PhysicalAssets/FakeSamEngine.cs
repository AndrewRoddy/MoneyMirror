using Microsoft.AspNetCore.Components;
using MoneyMirror.PhysicalAssets;

namespace MoneyMirror.Tests.PhysicalAssets;

public sealed class FakeSamEngine : ISamSegmentationEngine
{
    public int InitCalls { get; private set; }
    public int EncodeCalls { get; private set; }
    public int DecodePointCalls { get; private set; }
    public int DecodePointsCalls { get; private set; }
    public int ResetCalls { get; private set; }
    public int DisposeCalls { get; private set; }

    public SamSegmentationResult Result { get; set; } =
        new(
            [new(0.2, 0.2), new(0.8, 0.2), new(0.8, 0.8), new(0.2, 0.8)],
            new(0.2, 0.2, 0.6, 0.6),
            0.96,
            100,
            100,
            12.5
        );

    public ValueTask<SamEngineStatus> InitializeAsync(CancellationToken cancellationToken = default)
    {
        InitCalls++;
        return ValueTask.FromResult(new SamEngineStatus("ready", "webgpu", false, 0, 0));
    }

    public ValueTask<SamEngineStatus> GetStatusAsync(
        CancellationToken cancellationToken = default
    ) => ValueTask.FromResult(new SamEngineStatus("ready", "webgpu", true, 1024, 768));

    public ValueTask<SamEncodeResult> EncodeFrameAsync(
        ElementReference frameSource,
        CancellationToken cancellationToken = default
    )
    {
        EncodeCalls++;
        return ValueTask.FromResult(new SamEncodeResult(25.0, "webgpu", 1280, 720));
    }

    public ValueTask<SamSegmentationResult> DecodePointAsync(
        double x,
        double y,
        SamPromptType type = SamPromptType.Positive,
        CancellationToken cancellationToken = default
    )
    {
        DecodePointCalls++;
        return ValueTask.FromResult(Result);
    }

    public ValueTask<SamSegmentationResult> DecodePointsAsync(
        IReadOnlyList<SamPointPrompt> points,
        CancellationToken cancellationToken = default
    )
    {
        DecodePointsCalls++;
        return ValueTask.FromResult(Result);
    }

    public ValueTask ResetPriorAsync(CancellationToken cancellationToken = default)
    {
        ResetCalls++;
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        DisposeCalls++;
        return ValueTask.CompletedTask;
    }
}
