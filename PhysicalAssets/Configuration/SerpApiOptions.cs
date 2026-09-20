namespace MoneyMirror.PhysicalAssets.Configuration;

/// <summary>API key and Google Search endpoint configuration for SerpApi.</summary>
public sealed class SerpApiOptions
{
    public const string SectionName = "SerpApi";

    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://serpapi.com/search.json";
    public string CountryCode { get; set; } = "us";
    public string Language { get; set; } = "en";
}
