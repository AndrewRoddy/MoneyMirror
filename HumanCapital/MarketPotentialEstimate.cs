namespace MoneyMirror.HumanCapital;

/// <summary>
/// A compensation range grounded in real BLS wage data, packaged with the
/// evidence behind it - the occupation it's for, the source BLS series,
/// the date range covered, and the raw observations - so the result can be
/// shown transparently rather than as a bare number. #99 later adds an
/// LLM-authored prose explanation on top of this; it doesn't touch or
/// recompute anything here.
/// </summary>
public record MarketPotentialEstimate(
    string Occupation,
    IReadOnlyList<string> SeriesIds,
    BlsCompensationRange Range,
    int StartYear,
    int EndYear,
    IReadOnlyList<BlsWageObservation> Evidence);
