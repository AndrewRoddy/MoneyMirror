using Bunit;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MoneyMirror.Data;
using MoneyMirror.HumanCapital;
using HumanCapitalPage = MoneyMirror.Features.HumanCapital.HumanCapital;

namespace MoneyMirror.Tests.HumanCapital;

/// <summary>
/// #264: this page used to only ever populate its editable sections from a
/// freshly-uploaded resume, so an already-saved profile was invisible on
/// revisit, and there was no way to create one at all without a resume file.
/// </summary>
public sealed class HumanCapitalPageTests : IDisposable
{
    private readonly BunitContext _context = new();
    private readonly SqliteConnection _connection = new("Filename=:memory:");
    private readonly MoneyMirrorDbContext _db;
    private readonly EfProfessionalProfileRepository _repository;

    private class NotUsedInTheseTestsValidator : IResumeUploadValidator
    {
        public ResumeUploadValidationResult Validate(string fileName, long fileSizeBytes) =>
            throw new InvalidOperationException("Not exercised by these tests.");
    }

    private class NotUsedInTheseTestsTextExtractionService : IResumeTextExtractionService
    {
        public Task<string> ExtractTextAsync(
            Stream fileStream,
            string fileName,
            CancellationToken cancellationToken = default
        ) => throw new InvalidOperationException("Not exercised by these tests.");
    }

    private class NotUsedInTheseTestsProfileExtractionService
        : IProfessionalProfileExtractionService
    {
        public Task<ProfessionalProfile> ExtractAsync(
            string resumeText,
            CancellationToken cancellationToken = default
        ) => throw new InvalidOperationException("Not exercised by these tests.");
    }

    public HumanCapitalPageTests()
    {
        _connection.Open();
        _db = new MoneyMirrorDbContext(
            new DbContextOptionsBuilder<MoneyMirrorDbContext>().UseSqlite(_connection).Options
        );
        _db.Database.EnsureCreated();
        _repository = new EfProfessionalProfileRepository(_db);

        _context.Services.AddSingleton<IProfessionalProfileRepository>(_repository);
        _context.Services.AddSingleton<IResumeUploadValidator>(new NotUsedInTheseTestsValidator());
        _context.Services.AddSingleton<IResumeTextExtractionService>(
            new NotUsedInTheseTestsTextExtractionService()
        );
        _context.Services.AddSingleton<IProfessionalProfileExtractionService>(
            new NotUsedInTheseTestsProfileExtractionService()
        );
        _context.Services.AddSingleton<IOptions<ResumeUploadOptions>>(
            Options.Create(new ResumeUploadOptions())
        );
    }

    public void Dispose()
    {
        _context.Dispose();
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public void NoProfileSaved_ShowsStartFromScratchOption()
    {
        var page = _context.Render<HumanCapitalPage>();

        Assert.NotNull(page.Find("button:contains('Start from scratch')"));
        Assert.Empty(page.FindAll("h2:contains('Your Professional Profile')"));
    }

    [Fact]
    public void StartFromScratch_RevealsEditableSectionsWithNoUpload()
    {
        var page = _context.Render<HumanCapitalPage>();

        page.Find("button:contains('Start from scratch')").Click();

        Assert.NotEmpty(page.FindAll("h2:contains('Your Professional Profile')"));
        Assert.NotNull(page.Find("button:contains('Save Corrections')"));

        // Sections start empty ("+ Add" builds a row) rather than
        // pre-populated - confirm a fresh row can actually be added.
        page.FindAll("button:contains('+ Add')")[0].Click();
        Assert.NotNull(page.Find("input[placeholder='Institution']"));
    }

    [Fact]
    public async Task ProfileAlreadySaved_IsShownWithoutRequiringAReupload()
    {
        await _repository.SaveAsync(
            ProfessionalProfile.Empty with
            {
                Skills = [new Skill("C#", null)],
            }
        );

        var page = _context.Render<HumanCapitalPage>();

        Assert.NotEmpty(page.FindAll("h2:contains('Your Professional Profile')"));
        Assert.Equal("C#", page.Find("input[placeholder='Skill']").GetAttribute("value"));

        // Already has a profile, so "start from scratch" isn't offered - the
        // form above already lets them add/edit everything.
        Assert.Empty(page.FindAll("button:contains('Start from scratch')"));
    }
}
