namespace MoneyMirror.HumanCapital;

/// <summary>
/// An estimated compensation range for a set of matched occupations.
/// </summary>
/// <param name="IsAiEstimated">
/// True for this MVP placeholder: the range came from the LLM's judgment,
/// not from a real wage-data source. Never present this to the user (or
/// persist it) as if it were grounded - #148 is a deliberate, temporary
/// stand-in for the real #24 pipeline (real wage-data retrieval +
/// deterministic C# aggregation), which sets this false.
/// </param>
public record CompensationEstimate(
    decimal MinUsd,
    decimal MaxUsd,
    string Explanation,
    bool IsAiEstimated);
