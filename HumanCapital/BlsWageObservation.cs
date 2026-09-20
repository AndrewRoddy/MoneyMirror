namespace MoneyMirror.HumanCapital;

/// <summary>
/// One data point from a BLS time series - e.g. a monthly or annual wage
/// observation for a given series/occupation. Raw data only; no
/// aggregation or interpretation happens here (see #97).
/// </summary>
public record BlsWageObservation(
    string SeriesId,
    string Year,
    string Period,
    string PeriodName,
    decimal? Value,
    IReadOnlyList<string> Footnotes);
