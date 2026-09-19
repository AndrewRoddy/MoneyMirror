namespace PittMoney.Ai.Configuration;

/// <summary>
/// Configuration for the multimodal vision model provider used for physical-asset
/// detection, bound from the "Ai:VisionModel" section.
/// </summary>
public class VisionModelOptions
{
    public const string SectionName = "Ai:VisionModel";

    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://integrate.api.nvidia.com/v1";
    public string Model { get; set; } = "meta/llama-3.2-11b-vision-instruct";
}
