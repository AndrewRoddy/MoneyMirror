using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MoneyMirror.Data;
using DataEntities = MoneyMirror.Data.Entities;

namespace MoneyMirror.HumanCapital;

/// <summary>
/// <see cref="IProfessionalProfileRepository"/> implementation backed by
/// <see cref="MoneyMirrorDbContext"/>. Always writes to the single default
/// profile row (<see cref="DataEntities.ProfessionalProfile.DefaultId"/>) -
/// this app has no multi-profile/auth concept.
/// </summary>
public class EfProfessionalProfileRepository : IProfessionalProfileRepository
{
    private readonly MoneyMirrorDbContext _db;

    public EfProfessionalProfileRepository(MoneyMirrorDbContext db)
    {
        _db = db;
    }

    public async Task<ProfessionalProfile> GetAsync(CancellationToken cancellationToken = default)
    {
        var entity = await _db
            .ProfessionalProfiles.Include(p => p.EducationRecords)
            .Include(p => p.Certifications)
            .Include(p => p.Skills)
            .Include(p => p.Experiences)
            .FirstOrDefaultAsync(p => p.Id == DataEntities.ProfessionalProfile.DefaultId, cancellationToken);

        if (entity is null)
        {
            return ProfessionalProfile.Empty;
        }

        // Projects/Publications/Awards have no backing table yet, so they
        // always come back empty here - not fabricated, just not persisted.
        return new ProfessionalProfile(
            Education: entity
                .EducationRecords.Select(e => new EducationRecord(
                    e.InstitutionName,
                    e.Degree,
                    e.FieldOfStudy,
                    FormatDate(e.StartedAt),
                    FormatDate(e.CompletedAt)
                ))
                .ToList(),
            Certifications: entity
                .Certifications.Select(c => new Certification(c.Name, c.IssuingOrganization, FormatDate(c.IssuedAt)))
                .ToList(),
            Skills: entity.Skills.Select(s => new Skill(s.Name, s.ProficiencyLevel)).ToList(),
            Experience: entity
                .Experiences.Select(e => new Experience(
                    e.EmployerName,
                    e.JobTitle,
                    FormatDate(e.StartedAt),
                    FormatDate(e.EndedAt),
                    e.Description
                ))
                .ToList(),
            Projects: [],
            Publications: [],
            Awards: []
        );
    }

    private static string? FormatDate(DateTimeOffset? date) => date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public async Task SaveAsync(ProfessionalProfile profile, CancellationToken cancellationToken = default)
    {
        var entity = await _db
            .ProfessionalProfiles.Include(p => p.EducationRecords)
            .Include(p => p.Certifications)
            .Include(p => p.Skills)
            .Include(p => p.Experiences)
            .FirstOrDefaultAsync(p => p.Id == DataEntities.ProfessionalProfile.DefaultId, cancellationToken);

        if (entity is null)
        {
            entity = new DataEntities.ProfessionalProfile
            {
                Id = DataEntities.ProfessionalProfile.DefaultId,
                DisplayName = "Default Profile",
            };
            _db.ProfessionalProfiles.Add(entity);
        }

        // Full re-save: replace every child section wholesale rather than
        // diffing, since a resume re-upload/re-review is a full replacement,
        // not an incremental edit.
        _db.EducationRecords.RemoveRange(entity.EducationRecords);
        _db.Certifications.RemoveRange(entity.Certifications);
        _db.Skills.RemoveRange(entity.Skills);
        _db.Experiences.RemoveRange(entity.Experiences);

        entity.UpdatedAt = DateTimeOffset.UtcNow;

        var educationRecords = profile
            .Education.Select(e => new DataEntities.EducationRecord
            {
                InstitutionName = e.Institution,
                Degree = e.Degree,
                FieldOfStudy = e.FieldOfStudy,
                StartedAt = TryParseLooseDate(e.StartDate),
                CompletedAt = TryParseLooseDate(e.EndDate),
            })
            .ToList();

        var certifications = profile
            .Certifications.Select(c => new DataEntities.Certification
            {
                Name = c.Name,
                IssuingOrganization = c.IssuingOrganization,
                IssuedAt = TryParseLooseDate(c.IssueDate),
            })
            .ToList();

        var skills = profile.Skills.Select(s => new DataEntities.Skill { Name = s.Name }).ToList();

        // Experience.StartedAt is required by the schema; a resume entry
        // with no parseable start date is skipped rather than given a
        // fabricated one.
        var experiences = profile
            .Experience.Select(e => (Entry: e, StartedAt: TryParseLooseDate(e.StartDate)))
            .Where(x => x.StartedAt is not null)
            .Select(x => new DataEntities.Experience
            {
                EmployerName = x.Entry.Organization,
                JobTitle = x.Entry.Title ?? string.Empty,
                Description = x.Entry.Description,
                StartedAt = x.StartedAt!.Value,
                EndedAt = TryParseLooseDate(x.Entry.EndDate),
            })
            .ToList();

        // Must explicitly Add these rather than just assigning the navigation
        // property: each entity's Id is already a non-default Guid (set by
        // its own `= Guid.NewGuid()` property initializer), so EF Core's
        // default "is this new" heuristic mistakes a bare navigation-property
        // assignment for an update to an existing row instead of an insert,
        // producing an UPDATE that matches 0 rows and throws
        // DbUpdateConcurrencyException. Explicit AddRange forces Added state.
        _db.EducationRecords.AddRange(educationRecords);
        _db.Certifications.AddRange(certifications);
        _db.Skills.AddRange(skills);
        _db.Experiences.AddRange(experiences);

        entity.EducationRecords = educationRecords;
        entity.Certifications = certifications;
        entity.Skills = skills;
        entity.Experiences = experiences;

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static readonly string[] LooseDateFormats =
    [
        "yyyy",
        "MMM yyyy",
        "MMMM yyyy",
        "M/yyyy",
        "MM/yyyy",
        "yyyy-MM",
    ];

    /// <summary>
    /// Best-effort parse of the loose date strings resumes/LLM extraction
    /// produce (e.g. "2021", "Jun 2022"). "Present"/"Current"/"Now" and
    /// anything unparseable become null - never a fabricated date.
    /// </summary>
    private static DateTimeOffset? TryParseLooseDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var trimmed = raw.Trim();
        if (
            trimmed.Equals("present", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("current", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("now", StringComparison.OrdinalIgnoreCase)
        )
        {
            return null;
        }

        if (DateTimeOffset.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return parsed;
        }

        foreach (var format in LooseDateFormats)
        {
            if (DateTime.TryParseExact(trimmed, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            {
                return new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc));
            }
        }

        return null;
    }
}
