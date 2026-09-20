namespace MoneyMirror.PhysicalAssets;

/// <summary>Thrown when SerpApi cannot return comparable Google search results.</summary>
public sealed class SerpApiMarketDataException : Exception
{
    public SerpApiMarketDataException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}
