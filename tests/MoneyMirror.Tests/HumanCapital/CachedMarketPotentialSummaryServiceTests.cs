using Microsoft.Extensions.Caching.Memory;
using MoneyMirror.HumanCapital;

namespace MoneyMirror.Tests.HumanCapital;

public class CachedMarketPotentialSummaryServiceTests
{
    private static readonly ProfessionalProfile SampleProfile = ProfessionalProfile.Empty with
    {
        Skills = [new Skill("C#", null)],
        Experience = [new Experience("Acme Corp", "Backend Engineer", "2022", null, "Built APIs")],
    };

    private static readonly OccupationMatch SampleMatch = new(
        "Software Engineer",
        "Matches backend experience.",
        IsAiEstimated: true,
        KeySkills: ["C#"]
    );

    private static readonly CompensationEstimate SampleCompensation = new(
        85000m,
        130000m,
        "Typical range for the matched occupations.",
        IsAiEstimated: true
    );

    private class FakeProfileRepository(ProfessionalProfile profile)
        : IProfessionalProfileRepository
    {
        public ProfessionalProfile Profile { get; set; } = profile;

        public Task<ProfessionalProfile> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Profile);

        public Task SaveAsync(
            ProfessionalProfile profile,
            CancellationToken cancellationToken = default
        )
        {
            Profile = profile;
            return Task.CompletedTask;
        }
    }

    private class CountingOccupationMatchingService : IOccupationMatchingService
    {
        public int CallCount { get; private set; }

        public Task<IReadOnlyList<OccupationMatch>> MatchAsync(
            ProfessionalProfile profile,
            CancellationToken cancellationToken = default
        )
        {
            CallCount++;
            return Task.FromResult<IReadOnlyList<OccupationMatch>>([SampleMatch]);
        }
    }

    private class CountingCompensationService : ICompensationEstimationService
    {
        public int CallCount { get; private set; }

        public Task<CompensationEstimate> EstimateAsync(
            IReadOnlyList<OccupationMatch> occupations,
            CancellationToken cancellationToken = default
        )
        {
            CallCount++;
            return Task.FromResult(SampleCompensation);
        }
    }

    private static CachedMarketPotentialSummaryService CreateService(
        FakeProfileRepository profileRepository,
        CountingOccupationMatchingService occupationMatchingService,
        CountingCompensationService compensationService
    ) =>
        new(
            profileRepository,
            occupationMatchingService,
            compensationService,
            new MemoryCache(new MemoryCacheOptions())
        );

    [Fact]
    public async Task GetSummaryAsync_EmptyProfile_ReturnsHasProfileFalseWithoutCallingAiServices()
    {
        var occupationMatchingService = new CountingOccupationMatchingService();
        var compensationService = new CountingCompensationService();
        var service = CreateService(
            new FakeProfileRepository(ProfessionalProfile.Empty),
            occupationMatchingService,
            compensationService
        );

        var summary = await service.GetSummaryAsync();

        Assert.False(summary.HasProfile);
        Assert.Empty(summary.Occupations);
        Assert.Null(summary.Compensation);
        Assert.Equal(0, occupationMatchingService.CallCount);
        Assert.Equal(0, compensationService.CallCount);
    }

    [Fact]
    public async Task GetSummaryAsync_CalledTwiceForSameProfile_OnlyComputesOnce()
    {
        var occupationMatchingService = new CountingOccupationMatchingService();
        var compensationService = new CountingCompensationService();
        var service = CreateService(
            new FakeProfileRepository(SampleProfile),
            occupationMatchingService,
            compensationService
        );

        var first = await service.GetSummaryAsync();
        var second = await service.GetSummaryAsync();

        Assert.Equal(1, occupationMatchingService.CallCount);
        Assert.Equal(1, compensationService.CallCount);
        Assert.Equal(first.Compensation, second.Compensation);
        Assert.Equal(first.Occupations, second.Occupations);
    }

    [Fact]
    public async Task GetSummaryAsync_ProfileEditedBetweenCalls_RecomputesForNewContent()
    {
        var occupationMatchingService = new CountingOccupationMatchingService();
        var compensationService = new CountingCompensationService();
        var profileRepository = new FakeProfileRepository(SampleProfile);
        var service = CreateService(
            profileRepository,
            occupationMatchingService,
            compensationService
        );

        await service.GetSummaryAsync();

        profileRepository.Profile = SampleProfile with
        {
            Skills = [.. SampleProfile.Skills, new Skill("SQL", null)],
        };

        await service.GetSummaryAsync();

        Assert.Equal(2, occupationMatchingService.CallCount);
        Assert.Equal(2, compensationService.CallCount);
    }
}
