namespace MoneyMirror.HumanCapital;

/// <summary>Raw fields returned by O*NET's occupation report overview endpoint.</summary>
public sealed record OnetOccupation(
    string Code,
    string Title,
    string Description,
    IReadOnlyList<string> SampleReportedTitles
);
