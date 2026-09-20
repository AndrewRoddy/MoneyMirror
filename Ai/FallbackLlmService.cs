namespace MoneyMirror.Ai;

/// <summary>
/// <see cref="ILlmService"/> implementation that tries <see cref="NemotronLlmService"/>
/// first and falls back to <see cref="ClaudeLlmService"/> whenever Nemotron is
/// unreachable, errors out, or returns something unusable.
/// <para>
/// Nemotron stays the primary provider - Claude only covers the gap during an NVIDIA
/// outage or a saturated queue <see cref="TransientFaultRetryHandler"/> could not
/// clear. A prompt is never sent to both; the fallback only fires after the primary
/// has already failed.
/// </para>
/// </summary>
public class FallbackLlmService : ILlmService
{
    private readonly NemotronLlmService _primary;
    private readonly ClaudeLlmService _fallback;

    public FallbackLlmService(NemotronLlmService primary, ClaudeLlmService fallback)
    {
        _primary = primary;
        _fallback = fallback;
    }

    public async Task<string> CompleteAsync(
        string prompt,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            return await _primary.CompleteAsync(prompt, cancellationToken);
        }
        catch (LlmServiceException)
        {
            return await _fallback.CompleteAsync(prompt, cancellationToken);
        }
    }
}
