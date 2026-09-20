namespace MoneyMirror.PhysicalAssets;

/// <summary>Thrown when eBay's Browse API cannot return comparable listings.</summary>
public sealed class EbayMarketDataException : Exception
{
    public EbayMarketDataException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}
