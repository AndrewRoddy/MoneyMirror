namespace MoneyMirror.HumanCapital;

public class OnetOccupationDataException : Exception
{
    public OnetOccupationDataException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}
