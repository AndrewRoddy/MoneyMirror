namespace MoneyMirror.HumanCapital;

/// <summary>
/// A candidate occupation matched to a professional profile.
/// </summary>
/// <param name="IsAiEstimated">
/// True for this MVP placeholder: the match came from the LLM's judgment,
/// not from a real labor-market data source. Never present this to the
/// user (or persist it) as if it were grounded - #142 is a deliberate,
/// temporary stand-in for the real #23 (ILaborMarketService) pipeline,
/// which sets this false.
/// </param>
public record OccupationMatch(string Title, string Explanation, bool IsAiEstimated);
