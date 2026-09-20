namespace MoneyMirror.HumanCapital;

/// <summary>
/// A compensation range computed deterministically from real BLS wage
/// observations - the grounded counterpart to the AI-estimated
/// <see cref="CompensationEstimate"/> (#148's MVP placeholder).
/// </summary>
public record BlsCompensationRange(decimal MinUsd, decimal MaxUsd, int ObservationCount);
