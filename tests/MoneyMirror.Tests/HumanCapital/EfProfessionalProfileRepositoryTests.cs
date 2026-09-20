using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MoneyMirror.Data;
using MoneyMirror.HumanCapital;

namespace MoneyMirror.Tests.HumanCapital;

/// <summary>
/// Integration tests: exercises real EF Core behavior (inserts, updates,
/// cascade-delete replace, relationship fix-up) against a real SQLite
/// database, since no PostgreSQL server is available in this environment.
/// No mocking of EF Core itself.
/// </summary>
public class EfProfessionalProfileRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly MoneyMirrorDbContext _db;
    private readonly EfProfessionalProfileRepository _repository;

    public EfProfessionalProfileRepositoryTests()
    {
        // A SQLite ":memory:" database only lives as long as its connection
        // stays open, so we own and dispose it ourselves.
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<MoneyMirrorDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new MoneyMirrorDbContext(options);
        _db.Database.EnsureCreated();

        _repository = new EfProfessionalProfileRepository(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private static readonly ProfessionalProfile SampleProfile = new(
        Education:
        [
            new EducationRecord(
                "University of Pittsburgh",
                "B.S.",
                "Computer Science",
                "2018",
                "2022"
            ),
        ],
        Certifications: [new Certification("AWS Certified", "Amazon", "2023")],
        Skills: [new Skill("C#", null), new Skill("SQL", null)],
        Experience: [new Experience("Acme Corp", "Engineer", "2022", "Present", "Built things")],
        Projects: [new Project("Side project", "A thing I built")],
        Publications: [],
        Awards: []
    );

    [Fact]
    public async Task SaveAsync_FirstSave_CreatesDefaultProfileWithAllSections()
    {
        await _repository.SaveAsync(SampleProfile);

        var entity = await _db
            .ProfessionalProfiles.Include(p => p.EducationRecords)
            .Include(p => p.Certifications)
            .Include(p => p.Skills)
            .Include(p => p.Experiences)
            .SingleAsync(p => p.Id == MoneyMirror.Data.Entities.ProfessionalProfile.DefaultId);

        Assert.Single(entity.EducationRecords);
        Assert.Equal("University of Pittsburgh", entity.EducationRecords.Single().InstitutionName);
        Assert.Equal(
            new DateTimeOffset(2018, 1, 1, 0, 0, 0, TimeSpan.Zero),
            entity.EducationRecords.Single().StartedAt
        );

        Assert.Single(entity.Certifications);
        Assert.Equal(2, entity.Skills.Count);

        Assert.Single(entity.Experiences);
        var experience = entity.Experiences.Single();
        Assert.Equal("Acme Corp", experience.EmployerName);
        Assert.Equal(new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero), experience.StartedAt);
        Assert.Null(experience.EndedAt); // "Present" is not a date
    }

    [Fact]
    public async Task SaveAsync_SecondSave_ReplacesPreviousSectionsRatherThanAppending()
    {
        await _repository.SaveAsync(SampleProfile);

        var updated = SampleProfile with { Skills = [new Skill("Only this one now", null)] };
        await _repository.SaveAsync(updated);

        var entity = await _db
            .ProfessionalProfiles.Include(p => p.Skills)
            .SingleAsync(p => p.Id == MoneyMirror.Data.Entities.ProfessionalProfile.DefaultId);

        Assert.Single(entity.Skills);
        Assert.Equal("Only this one now", entity.Skills.Single().Name);
    }

    [Fact]
    public async Task SaveAsync_ExperienceWithUnparseableStartDate_IsSkippedNotFabricated()
    {
        var profile = ProfessionalProfile.Empty with
        {
            Experience =
            [
                new Experience(
                    "Acme Corp",
                    "Engineer",
                    StartDate: null,
                    EndDate: null,
                    Description: null
                ),
            ],
        };

        await _repository.SaveAsync(profile);

        var entity = await _db
            .ProfessionalProfiles.Include(p => p.Experiences)
            .SingleAsync(p => p.Id == MoneyMirror.Data.Entities.ProfessionalProfile.DefaultId);

        Assert.Empty(entity.Experiences);
    }

    [Fact]
    public async Task SaveAsync_EmptyProfile_PersistsDefaultProfileWithNoSections()
    {
        await _repository.SaveAsync(ProfessionalProfile.Empty);

        var entity = await _db
            .ProfessionalProfiles.Include(p => p.EducationRecords)
            .Include(p => p.Certifications)
            .Include(p => p.Skills)
            .Include(p => p.Experiences)
            .SingleAsync(p => p.Id == MoneyMirror.Data.Entities.ProfessionalProfile.DefaultId);

        Assert.Empty(entity.EducationRecords);
        Assert.Empty(entity.Certifications);
        Assert.Empty(entity.Skills);
        Assert.Empty(entity.Experiences);
    }

    [Fact]
    public async Task GetAsync_NothingSavedYet_ReturnsEmpty()
    {
        var profile = await _repository.GetAsync();

        Assert.Empty(profile.Education);
        Assert.Empty(profile.Certifications);
        Assert.Empty(profile.Skills);
        Assert.Empty(profile.Experience);
        Assert.Empty(profile.Projects);
        Assert.Empty(profile.Publications);
        Assert.Empty(profile.Awards);
    }

    [Fact]
    public async Task GetAsync_AfterSave_RoundTripsEducationExperienceSkillsCertifications()
    {
        await _repository.SaveAsync(SampleProfile);

        var profile = await _repository.GetAsync();

        var education = Assert.Single(profile.Education);
        Assert.Equal("University of Pittsburgh", education.Institution);
        Assert.Equal("2018-01-01", education.StartDate);

        var experience = Assert.Single(profile.Experience);
        Assert.Equal("Acme Corp", experience.Organization);
        Assert.Null(experience.EndDate); // "Present" was never a parseable date

        Assert.Equal(2, profile.Skills.Count);
        Assert.Single(profile.Certifications);

        // Not persisted (no backing table) - always empty on read, not fabricated.
        Assert.Empty(profile.Projects);
        Assert.Empty(profile.Publications);
        Assert.Empty(profile.Awards);
    }

    [Fact]
    public async Task GetAsync_MultipleEntriesInMultipleCollections_ReturnsEachEntryExactlyOnce()
    {
        // #263: regression guard for AsSplitQuery() on the Include chain -
        // EF Core reconstructs the object graph correctly with or without
        // it, so this doesn't catch a correctness bug, but it does pin down
        // the shape callers depend on (one entry in, one entry out) across
        // that change.
        var profile = ProfessionalProfile.Empty with
        {
            Education =
            [
                new EducationRecord("University A", "B.S.", "CS", "2014", "2018"),
                new EducationRecord("University B", "M.S.", "CS", "2018", "2020"),
            ],
            Experience =
            [
                new Experience("Company A", "Engineer", "2018", "2020", null),
                new Experience("Company B", "Senior Engineer", "2020", null, null),
            ],
        };

        await _repository.SaveAsync(profile);
        var roundTripped = await _repository.GetAsync();

        Assert.Equal(2, roundTripped.Education.Count);
        Assert.Equal(2, roundTripped.Experience.Count);
    }
}
