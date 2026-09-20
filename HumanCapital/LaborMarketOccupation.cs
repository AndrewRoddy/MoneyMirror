namespace MoneyMirror.HumanCapital;

/// <summary>
/// Occupation information exposed by the labor-market integration.
/// </summary>
/// <remarks>
/// The description is the source's narrative of the occupation and its
/// typical work requirements. Matching and ranking remain separate concerns.
/// </remarks>
public sealed record LaborMarketOccupation(
    string Code,
    string Title,
    string Description,
    IReadOnlyList<string> SampleReportedTitles
);
