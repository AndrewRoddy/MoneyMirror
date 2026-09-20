namespace MoneyMirror.PhysicalAssets.Configuration;

/// <summary>Credentials and endpoint configuration for eBay Browse API.</summary>
public sealed class EbayOptions
{
    public const string SectionName = "Ebay";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.ebay.com";
    public string MarketplaceId { get; set; } = "EBAY_US";
}
