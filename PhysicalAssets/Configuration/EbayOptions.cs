namespace MoneyMirror.PhysicalAssets.Configuration;

/// <summary>Client-credentials app keys and Browse API endpoint configuration for eBay.
/// Defaults point at eBay's sandbox environment; swap both URLs to the production hosts
/// once the app has production-approved keys (sandbox listings are seeded test data, not
/// a real catalog).</summary>
public sealed class EbayOptions
{
    public const string SectionName = "Ebay";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string AuthUrl { get; set; } = "https://api.sandbox.ebay.com/identity/v1/oauth2/token";
    public string SearchUrl { get; set; } =
        "https://api.sandbox.ebay.com/buy/browse/v1/item_summary/search";
    public string MarketplaceId { get; set; } = "EBAY_US";
    public int CacheDurationHours { get; set; } = 720;
    public string CacheDirectory { get; set; } = "App_Data/ebay-search-cache";
}
