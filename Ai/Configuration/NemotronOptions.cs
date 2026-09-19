namespace PittMoney.Ai.Configuration;

/// <summary>
/// Configuration for the NVIDIA Nemotron LLM provider, bound from the "Ai:Nemotron" section.
/// </summary>
public class NemotronOptions
{
    public const string SectionName = "Ai:Nemotron";

    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://integrate.api.nvidia.com/v1";
    public string Model { get; set; } = "nvidia/llama-3.1-nemotron-70b-instruct";
}
