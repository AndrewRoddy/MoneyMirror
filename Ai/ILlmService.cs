namespace MoneyMirror.Ai;

/// <summary>
/// Thin seam over the LLM provider (NVIDIA Nemotron): a prompt in, a completion out.
/// Feature code should depend on this interface, never on the concrete provider.
/// </summary>
public interface ILlmService
{
    /// <summary>
    /// Sends <paramref name="prompt"/> to the LLM and returns its text completion.
    /// </summary>
    /// <exception cref="LlmServiceException">
    /// The provider call failed, returned an error, or returned an unusable response.
    /// </exception>
    Task<string> CompleteAsync(string prompt, CancellationToken cancellationToken = default);
}
