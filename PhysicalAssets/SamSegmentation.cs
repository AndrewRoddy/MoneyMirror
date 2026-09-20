using Microsoft.JSInterop;

namespace MoneyMirror.PhysicalAssets;

public enum SamPromptType
{
    Negative = 0,
    Positive = 1,
    TopLeft = 2,
    BottomRight = 3,
}

public record SamPointPrompt(double X, double Y, SamPromptType Type = SamPromptType.Positive);

public record SamSegmentationResult(
    IReadOnlyList<NormalizedPoint> Polygon,
    BoundingBox Bounds,
    double Confidence,
    int MaskWidth,
    int MaskHeight,
    double ElapsedMs
);

public record SamEngineStatus(
    string State,
    string? Device,
    bool HasEmbeddings,
    int Width,
    int Height
);

public interface ISamSegmentationEngine : IAsyncDisposable
{
    ValueTask<SamEngineStatus> InitializeAsync(CancellationToken cancellationToken = default);

    ValueTask<SamEngineStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    ValueTask<SamSegmentationResult> DecodePointAsync(
        double x,
        double y,
        SamPromptType type = SamPromptType.Positive,
        CancellationToken cancellationToken = default
    );

    ValueTask<SamSegmentationResult> DecodePointsAsync(
        IReadOnlyList<SamPointPrompt> points,
        CancellationToken cancellationToken = default
    );

    ValueTask ResetPriorAsync(CancellationToken cancellationToken = default);
}

public class BlazorSamSegmentationEngine : ISamSegmentationEngine
{
    private const string ModulePath = "./js/mobile-sam.js";
    private readonly IJSRuntime _js;
    private IJSObjectReference? _module;

    public BlazorSamSegmentationEngine(IJSRuntime js)
    {
        _js = js;
    }

    public async ValueTask<SamEngineStatus> InitializeAsync(
        CancellationToken cancellationToken = default
    )
    {
        var module = await GetModuleAsync(cancellationToken);
        return await module.InvokeAsync<SamEngineStatus>("initEngine", cancellationToken);
    }

    public async ValueTask<SamEngineStatus> GetStatusAsync(
        CancellationToken cancellationToken = default
    )
    {
        var module = await GetModuleAsync(cancellationToken);
        return await module.InvokeAsync<SamEngineStatus>("getEngineStatus", cancellationToken);
    }

    public async ValueTask<SamSegmentationResult> DecodePointAsync(
        double x,
        double y,
        SamPromptType type = SamPromptType.Positive,
        CancellationToken cancellationToken = default
    )
    {
        var module = await GetModuleAsync(cancellationToken);
        return await module.InvokeAsync<SamSegmentationResult>(
            "decodePoint",
            cancellationToken,
            x,
            y,
            (int)type
        );
    }

    public async ValueTask<SamSegmentationResult> DecodePointsAsync(
        IReadOnlyList<SamPointPrompt> points,
        CancellationToken cancellationToken = default
    )
    {
        var module = await GetModuleAsync(cancellationToken);
        var jsPoints = points
            .Select(p => new
            {
                x = p.X,
                y = p.Y,
                type = (int)p.Type,
            })
            .ToArray();

        return await module.InvokeAsync<SamSegmentationResult>(
            "decodeMultiPoints",
            cancellationToken,
            jsPoints
        );
    }

    public async ValueTask ResetPriorAsync(CancellationToken cancellationToken = default)
    {
        var module = await GetModuleAsync(cancellationToken);
        await module.InvokeVoidAsync("resetPrior", cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                await _module.InvokeVoidAsync("disposeEngine");
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // Circuit already disconnected
            }
            finally
            {
                _module = null;
            }
        }
    }

    private async ValueTask<IJSObjectReference> GetModuleAsync(CancellationToken cancellationToken)
    {
        if (_module is not null)
        {
            return _module;
        }

        _module = await _js.InvokeAsync<IJSObjectReference>(
            "import",
            cancellationToken,
            ModulePath
        );
        return _module;
    }
}
