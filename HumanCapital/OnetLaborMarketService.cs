namespace MoneyMirror.HumanCapital;

/// <summary>
/// O*NET-backed implementation of the labor-market integration seam.
/// </summary>
public sealed class OnetLaborMarketService : ILaborMarketService
{
    private readonly IOnetOccupationDataService _onetOccupationDataService;

    public OnetLaborMarketService(IOnetOccupationDataService onetOccupationDataService)
    {
        _onetOccupationDataService = onetOccupationDataService;
    }

    public async Task<LaborMarketOccupation> GetOccupationAsync(
        string occupationCode,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(occupationCode);

        var occupation = await _onetOccupationDataService.GetOccupationAsync(
            occupationCode,
            cancellationToken
        );

        return new LaborMarketOccupation(
            occupation.Code,
            occupation.Title,
            occupation.Description,
            occupation.SampleReportedTitles
        );
    }
}
