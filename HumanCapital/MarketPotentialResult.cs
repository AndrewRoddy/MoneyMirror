namespace MoneyMirror.HumanCapital;

/// <summary>
/// A BLS-grounded compensation estimate together with Nemotron's prose
/// explanation of it - the fully wired-up result of #96 (real wage data)
/// -> #97 (deterministic aggregation) -> #98 (evidence DTO) -> #99 (LLM
/// explanation).
/// </summary>
public record MarketPotentialResult(MarketPotentialEstimate Estimate, string Explanation);
