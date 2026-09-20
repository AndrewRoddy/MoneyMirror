using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;

namespace MoneyMirror.HumanCapital;

/// <summary>
/// <see cref="IMarketPotentialSummaryService"/> implementation that caches
/// the occupation-matching + compensation-estimation result by the profile
/// content that fed it.
/// <para>
/// #260: Dashboard and Market Potential each called
/// <see cref="IOccupationMatchingService"/> and
/// <see cref="ICompensationEstimationService"/> directly, so visiting either
/// page re-ran both live LLM calls from scratch every time - even when
/// nothing about the profile had changed since the last visit, and even
/// when the other page had just computed the identical answer. Keying the
/// cache on a fingerprint of the exact profile fields the LLM prompt is
/// built from (see <see cref="AiSuggestedOccupationMatchingService.BuildPrompt"/>)
/// means an edited profile naturally misses the cache on its next load,
/// with no explicit invalidation call required.
/// </para>
/// </summary>
public class CachedMarketPotentialSummaryService : IMarketPotentialSummaryService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    private readonly IProfessionalProfileRepository _profileRepository;
    private readonly IOccupationMatchingService _occupationMatchingService;
    private readonly ICompensationEstimationService _compensationService;
    private readonly IMemoryCache _cache;

    public CachedMarketPotentialSummaryService(
        IProfessionalProfileRepository profileRepository,
        IOccupationMatchingService occupationMatchingService,
        ICompensationEstimationService compensationService,
        IMemoryCache cache
    )
    {
        _profileRepository = profileRepository;
        _occupationMatchingService = occupationMatchingService;
        _compensationService = compensationService;
        _cache = cache;
    }

    public async Task<MarketPotentialSummary> GetSummaryAsync(
        CancellationToken cancellationToken = default
    )
    {
        var profile = await _profileRepository.GetAsync(cancellationToken);

        if (
            profile.Education.Count == 0
            && profile.Experience.Count == 0
            && profile.Skills.Count == 0
        )
        {
            return new MarketPotentialSummary(
                profile,
                HasProfile: false,
                Occupations: [],
                Compensation: null
            );
        }

        var cacheKey = $"market-potential-summary:{Fingerprint(profile)}";

        if (_cache.TryGetValue(cacheKey, out CachedResult? cached) && cached is not null)
        {
            return new MarketPotentialSummary(
                profile,
                HasProfile: true,
                cached.Occupations,
                cached.Compensation
            );
        }

        var occupations = await _occupationMatchingService.MatchAsync(profile, cancellationToken);
        var compensation = await _compensationService.EstimateAsync(occupations, cancellationToken);

        _cache.Set(cacheKey, new CachedResult(occupations, compensation), CacheDuration);

        return new MarketPotentialSummary(profile, HasProfile: true, occupations, compensation);
    }

    /// <summary>
    /// A stable digest of exactly the profile fields
    /// <see cref="AiSuggestedOccupationMatchingService.BuildPrompt"/> feeds to the LLM
    /// - anything not in that prompt can't change the answer, so it's left out here.
    /// </summary>
    private static string Fingerprint(ProfessionalProfile profile)
    {
        var text = new StringBuilder();

        foreach (var skill in profile.Skills)
        {
            text.Append("skill:").Append(skill.Name).Append('|');
        }

        foreach (var experience in profile.Experience)
        {
            text.Append("exp:")
                .Append(experience.Title)
                .Append(':')
                .Append(experience.Organization)
                .Append(':')
                .Append(experience.Description)
                .Append('|');
        }

        foreach (var education in profile.Education)
        {
            text.Append("edu:")
                .Append(education.Degree)
                .Append(':')
                .Append(education.FieldOfStudy)
                .Append(':')
                .Append(education.Institution)
                .Append('|');
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString()));
        return Convert.ToHexString(hash);
    }

    private sealed record CachedResult(
        IReadOnlyList<OccupationMatch> Occupations,
        CompensationEstimate Compensation
    );
}
