namespace MoneyMirror.HumanCapital.Configuration;

public class BlsOptions
{
    public const string SectionName = "Bls";

    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.bls.gov/publicAPI/v2/timeseries/data/"; 
}