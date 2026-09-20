namespace MoneyMirror.HumanCapital.Configuration;

public class OnetOptions
{
    public const string SectionName = "Onet";

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// API root only. Specific O*NET Web Services paths are appended by the client.
    /// </summary>
    public string BaseUrl { get; set; } = "https://services.onetcenter.org/";
}
