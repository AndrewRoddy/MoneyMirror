namespace MoneyMirror.HumanCapital;

/// <summary>Raw fields returned by O*NET's occupation report overview endpoint.</summary>
public sealed record OnetOccupation(
    string Code,
    string Title,
    string Description,
    IReadOnlyList<string> SampleReportedTitles
);

/// <summary>A title and O*NET-SOC code returned by the O*NET keyword search.</summary>
public sealed record OnetOccupationSearchResult(string Code, string Title);
