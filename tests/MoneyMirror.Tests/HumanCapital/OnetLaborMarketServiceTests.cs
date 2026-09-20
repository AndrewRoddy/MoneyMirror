using MoneyMirror.HumanCapital;

namespace MoneyMirror.Tests.HumanCapital;

public class OnetLaborMarketServiceTests
{
    [Fact]
    public async Task GetOccupationAsync_MapsSourceOccupationData()
    {
        var source = new FakeOnetOccupationDataService(
            new OnetOccupation(
                "15-1252.00",
                "Software Developers",
                "Develop and maintain software applications.",
                ["Application Developer", "Software Engineer"]
            )
        );
        var service = new OnetLaborMarketService(source);

        var occupation = await service.GetOccupationAsync("15-1252.00");

        Assert.Equal("15-1252.00", occupation.Code);
        Assert.Equal("Software Developers", occupation.Title);
        Assert.Equal("Develop and maintain software applications.", occupation.Description);
        Assert.Equal(["Application Developer", "Software Engineer"], occupation.SampleReportedTitles);
        Assert.Equal("15-1252.00", source.LastCode);
    }

    [Fact]
    public async Task GetOccupationAsync_RejectsBlankCodeWithoutCallingSource()
    {
        var source = new FakeOnetOccupationDataService((OnetOccupation?)null);
        var service = new OnetLaborMarketService(source);

        await Assert.ThrowsAsync<ArgumentException>(() => service.GetOccupationAsync("  "));

        Assert.Null(source.LastCode);
    }

    [Fact]
    public async Task GetOccupationAsync_PropagatesSourceFailure()
    {
        var source = new FakeOnetOccupationDataService(
            new OnetOccupationDataException("O*NET unavailable")
        );
        var service = new OnetLaborMarketService(source);

        var exception = await Assert.ThrowsAsync<OnetOccupationDataException>(
            () => service.GetOccupationAsync("15-1252.00")
        );

        Assert.Equal("O*NET unavailable", exception.Message);
    }

    private sealed class FakeOnetOccupationDataService : IOnetOccupationDataService
    {
        private readonly OnetOccupation? _occupation;
        private readonly Exception? _exception;

        public FakeOnetOccupationDataService(OnetOccupation? occupation)
        {
            _occupation = occupation;
        }

        public FakeOnetOccupationDataService(Exception exception)
        {
            _exception = exception;
        }

        public string? LastCode { get; private set; }

        public Task<IReadOnlyList<OnetOccupationSearchResult>> SearchOccupationsAsync(
            string keyword,
            CancellationToken cancellationToken = default
        ) => Task.FromResult<IReadOnlyList<OnetOccupationSearchResult>>([]);

        public Task<IReadOnlyList<string>> GetOccupationSkillsAsync(
            string onetSocCode,
            CancellationToken cancellationToken = default
        ) => Task.FromResult<IReadOnlyList<string>>([]);

        public Task<OnetOccupation> GetOccupationAsync(
            string onetSocCode,
            CancellationToken cancellationToken = default
        )
        {
            LastCode = onetSocCode;
            if (_exception is not null)
            {
                return Task.FromException<OnetOccupation>(_exception);
            }

            return Task.FromResult(_occupation!);
        }
    }
}
