namespace MoneyMirror.HumanCapital.Configuration;

public class BlsOptions
{
    public const string SectionName = "Bls";

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// API root only (e.g. "https://api.bls.gov/publicAPI/v2/") - matches
    /// appsettings.json's convention. Callers append the specific endpoint
    /// path (e.g. "timeseries/data/"), the same pattern NemotronOptions/
    /// VisionModelOptions use.
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.bls.gov/publicAPI/v2/";
}
