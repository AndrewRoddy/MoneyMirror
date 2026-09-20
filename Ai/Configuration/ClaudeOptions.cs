namespace MoneyMirror.Ai.Configuration;

/// <summary>
/// Configuration for the Anthropic Claude LLM provider, bound from the "Ai:Claude"
/// section. Used only as <see cref="MoneyMirror.Ai.FallbackLlmService"/>'s backup -
/// see that type's remarks for why a second provider exists at all.
/// </summary>
public class ClaudeOptions
{
    public const string SectionName = "Ai:Claude";

    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.anthropic.com/v1";
    public string Model { get; set; } = "claude-sonnet-4-5";
    public string AnthropicVersion { get; set; } = "2023-06-01";

    /// <summary>
    /// Required only for API keys that are not scoped to a single workspace;
    /// Anthropic rejects those requests without this header. Left empty when the
    /// key is already workspace-scoped.
    /// </summary>
    public string WorkspaceId { get; set; } = string.Empty;
}
