using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace MoneyMirror.HumanCapital;

/// <summary>
/// Finds real occupations in O*NET and ranks them by overlap with profile evidence.
/// </summary>
public sealed partial class OnetOccupationMatchingService : IOccupationMatchingService
{
    private const int MaxSearchQueries = 4;
    private const int ResultsPerQuery = 4;
    private const int MaxCandidates = 12;
    private const int MaxMatches = 5;

    private static readonly HashSet<string> IgnoredTerms = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "and", "at", "by", "for", "from", "in", "into", "of", "on", "or", "the", "to", "with",
        "about", "across", "also", "been", "being", "build", "built", "company", "develop", "experience",
        "experienced", "focused", "including", "manage", "managed", "management", "new", "other", "over",
        "responsible", "role", "team", "using", "work", "worked", "working", "year", "years"
    };

    private readonly IOnetOccupationDataService _onet;
    private readonly ILogger<OnetOccupationMatchingService> _logger;

    public OnetOccupationMatchingService(
        IOnetOccupationDataService onet,
        ILogger<OnetOccupationMatchingService> logger
    )
    {
        _onet = onet;
        _logger = logger;
    }

    public async Task<IReadOnlyList<OccupationMatch>> MatchAsync(
        ProfessionalProfile profile,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(profile);

        var evidence = BuildEvidence(profile);
        if (evidence.Count == 0)
        {
            return [];
        }

        var queries = BuildSearchQueries(profile);
        if (queries.Count == 0)
        {
            return [];
        }

        try
        {
            var searchResults = await Task.WhenAll(
                queries.Select(query => _onet.SearchOccupationsAsync(query, cancellationToken))
            );
            var candidates = SelectCandidates(searchResults);
            if (candidates.Count == 0)
            {
                return [];
            }

            var occupations = await GetOccupationDetailsAsync(candidates, cancellationToken);
            var topMatches = occupations
                .Select(occupation => Rank(occupation, evidence))
                .Where(match => match.Score > 0)
                .OrderByDescending(match => match.Score)
                .ThenByDescending(match => match.MatchedEvidenceCount)
                .ThenBy(match => match.Occupation.Title, StringComparer.OrdinalIgnoreCase)
                .Take(MaxMatches)
                .ToList();
            var occupationSkills = await GetOccupationSkillsAsync(topMatches, cancellationToken);

            return topMatches
                .Select((match, index) => new OccupationMatch(
                    match.Occupation.Title,
                    BuildExplanation(match),
                    IsAiEstimated: false,
                    KeySkills: occupationSkills[index]
                ))
                .ToList();
        }
        catch (OnetOccupationDataException ex)
        {
            throw new OccupationMatchingException(
                "Failed to match occupations using O*NET occupation data.",
                ex
            );
        }
    }

    private static IReadOnlyList<string> BuildSearchQueries(ProfessionalProfile profile)
    {
        var queries = new List<string>();

        // Individual skill searches keep the O*NET keyword search focused and make
        // room for experience and education searches in the same bounded request set.
        queries.AddRange(
            profile.Skills
                .Select(skill => skill.Name)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(2)
        );
        queries.AddRange(
            profile.Experience
                .Select(experience => experience.Title)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(1)
        );

        var educationQuery = string.Join(
            " ",
            profile.Education
                .SelectMany(education => new[] { education.FieldOfStudy, education.Degree })
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(2)
        );
        if (!string.IsNullOrWhiteSpace(educationQuery))
        {
            queries.Add(educationQuery);
        }

        return queries
            .Select(query => query.Trim())
            .Where(query => query.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxSearchQueries)
            .ToList();
    }

    private static List<OnetOccupationSearchResult> SelectCandidates(
        IReadOnlyList<IReadOnlyList<OnetOccupationSearchResult>> searchResults
    )
    {
        var candidates = new List<OnetOccupationSearchResult>();
        var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Take results in rounds so one broad query cannot crowd the other profile
        // sections out of the detail lookups.
        for (var resultIndex = 0; resultIndex < ResultsPerQuery; resultIndex++)
        {
            foreach (var results in searchResults)
            {
                if (
                    resultIndex < results.Count
                    && seenCodes.Add(results[resultIndex].Code)
                )
                {
                    candidates.Add(results[resultIndex]);
                    if (candidates.Count == MaxCandidates)
                    {
                        return candidates;
                    }
                }
            }
        }

        return candidates;
    }

    private async Task<IReadOnlyList<OnetOccupation>> GetOccupationDetailsAsync(
        IReadOnlyList<OnetOccupationSearchResult> candidates,
        CancellationToken cancellationToken
    )
    {
        using var concurrencyLimit = new SemaphoreSlim(4);
        var requests = candidates.Select(async candidate =>
        {
            await concurrencyLimit.WaitAsync(cancellationToken);
            try
            {
                return await _onet.GetOccupationAsync(candidate.Code, cancellationToken);
            }
            finally
            {
                concurrencyLimit.Release();
            }
        });

        return await Task.WhenAll(requests);
    }

    private async Task<IReadOnlyList<string>[]> GetOccupationSkillsAsync(
        IReadOnlyList<ScoredOccupation> matches,
        CancellationToken cancellationToken
    )
    {
        using var concurrencyLimit = new SemaphoreSlim(4);
        var requests = matches.Select(async match =>
        {
            await concurrencyLimit.WaitAsync(cancellationToken);
            try
            {
                return await _onet.GetOccupationSkillsAsync(match.Occupation.Code, cancellationToken);
            }
            catch (OnetOccupationDataException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Could not load O*NET skills for occupation {OccupationCode}; returning the occupation match without skill data.",
                    match.Occupation.Code
                );
                return Array.Empty<string>();
            }
            finally
            {
                concurrencyLimit.Release();
            }
        });

        return await Task.WhenAll(requests);
    }

    private static List<ProfileEvidence> BuildEvidence(ProfessionalProfile profile)
    {
        var evidence = new List<ProfileEvidence>();
        evidence.AddRange(profile.Skills.Select(skill => new ProfileEvidence("skills", skill.Name, 3.0)));
        evidence.AddRange(
            profile.Experience.Select(experience => new ProfileEvidence("experience", experience.Title, 2.5))
        );
        evidence.AddRange(
            profile.Experience.Select(experience => new ProfileEvidence("experience", experience.Description, 1.0))
        );
        evidence.AddRange(
            profile.Education.Select(education => new ProfileEvidence("education", education.FieldOfStudy, 2.0))
        );
        evidence.AddRange(
            profile.Education.Select(education => new ProfileEvidence("education", education.Degree, 1.0))
        );

        return evidence.Where(item => item.Terms.Count > 0).ToList();
    }

    private static ScoredOccupation Rank(OnetOccupation occupation, IReadOnlyList<ProfileEvidence> evidence)
    {
        var titleTerms = Tokenize(occupation.Title);
        var reportedTitleTerms = Tokenize(string.Join(" ", occupation.SampleReportedTitles));
        var descriptionTerms = Tokenize(occupation.Description);
        var corpusTerms = titleTerms
            .Concat(reportedTitleTerms)
            .Concat(descriptionTerms)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var matched = new List<MatchedEvidence>();
        var score = 0.0;
        foreach (var item in evidence)
        {
            var overlaps = item.Terms.Where(corpusTerms.Contains).ToList();
            if (overlaps.Count == 0)
            {
                continue;
            }

            var coverage = (double)overlaps.Count / item.Terms.Count;
            var surfaceMultiplier = overlaps.Any(titleTerms.Contains)
                ? 2.0
                : overlaps.Any(reportedTitleTerms.Contains)
                    ? 1.5
                    : 1.0;
            var itemScore = item.Weight * coverage * surfaceMultiplier;
            score += itemScore;
            matched.Add(new MatchedEvidence(item.Section, item.Text!, itemScore));
        }

        return new ScoredOccupation(occupation, score, matched);
    }

    private static string BuildExplanation(ScoredOccupation match)
    {
        var source = match.MatchedEvidence.Any(item =>
                Tokenize(match.Occupation.Title).Intersect(Tokenize(item.Text), StringComparer.OrdinalIgnoreCase).Any()
            )
            ? "title"
            : match.MatchedEvidence.Any(item =>
                Tokenize(string.Join(" ", match.Occupation.SampleReportedTitles))
                    .Intersect(Tokenize(item.Text), StringComparer.OrdinalIgnoreCase)
                    .Any()
            )
                ? "reported job titles"
                : "description";

        var sections = match.MatchedEvidence
            .GroupBy(item => item.Section)
            .OrderByDescending(group => group.Max(item => item.Score))
            .Select(group => $"{group.Key} ({string.Join(", ", group
                .OrderByDescending(item => item.Score)
                .Select(item => Shorten(item.Text))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(2))})")
            .ToList();

        return $"O*NET's {source} matches profile {string.Join(" and ", sections)}.";
    }

    private static string Shorten(string value)
    {
        var singleLine = string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return singleLine.Length <= 60 ? singleLine : $"{singleLine[..57]}…";
    }

    private static HashSet<string> Tokenize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return WordRegex()
            .Matches(text.ToLowerInvariant())
            .Select(match => match.Value.Trim('.'))
            .Where(term => term.Length > 1 && !IgnoredTerms.Contains(term))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    [GeneratedRegex(@"[a-z0-9][a-z0-9+#.]*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WordRegex();

    private sealed record ProfileEvidence(string Section, string? Text, double Weight)
    {
        public HashSet<string> Terms { get; } = Tokenize(Text);
    }

    private sealed record MatchedEvidence(string Section, string Text, double Score);

    private sealed record ScoredOccupation(
        OnetOccupation Occupation,
        double Score,
        IReadOnlyList<MatchedEvidence> MatchedEvidence
    )
    {
        public int MatchedEvidenceCount => MatchedEvidence.Count;
    }
}
