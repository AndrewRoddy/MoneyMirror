using System.Globalization;
using MoneyMirror.Ai;
using MoneyMirror.Ai.Configuration;
using MoneyMirror.Components;
using MoneyMirror.HumanCapital;
using MoneyMirror.PhysicalAssets;
using Microsoft.EntityFrameworkCore;
using MoneyMirror.Data;
using MoneyMirror.Features.Financial;
using MoneyMirror.HumanCapital.Configuration;

// Containers start with no LANG/LC_ALL, so .NET falls back to the invariant culture
// and renders currency as "¤" instead of "$". Pin the formatting culture so money
// looks the same in Docker as it does on a dev machine.
var appCulture = new CultureInfo("en-US");
CultureInfo.DefaultThreadCurrentCulture = appCulture;
CultureInfo.DefaultThreadCurrentUICulture = appCulture;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

builder.Services.Configure<NemotronOptions>(
    builder.Configuration.GetSection(NemotronOptions.SectionName)
);
builder.Services.Configure<BlsOptions>(
    builder.Configuration.GetSection(BlsOptions.SectionName)
);
builder.Services.Configure<OnetOptions>(
    builder.Configuration.GetSection(OnetOptions.SectionName)
);

builder.Services.Configure<VisionModelOptions>(
    builder.Configuration.GetSection(VisionModelOptions.SectionName));
builder.Services.Configure<ResumeUploadOptions>(
    builder.Configuration.GetSection(ResumeUploadOptions.SectionName));
builder.Services.Configure<ImageUploadOptions>(
    builder.Configuration.GetSection(ImageUploadOptions.SectionName));

builder.Services.AddHttpClient<ILlmService, NemotronLlmService>();
builder.Services.AddHttpClient<IVisionService, NvidiaVisionService>();
builder.Services.AddHttpClient<IBlsWageDataService, BlsWageDataService>();
builder.Services.AddScoped<IMarketPotentialExplanationService, NemotronMarketPotentialExplanationService>();
builder.Services.AddScoped<IMarketPotentialPipeline, MarketPotentialPipeline>();
builder.Services.AddHttpClient<IOnetOccupationDataService, OnetOccupationDataService>();
builder.Services.AddScoped<ILaborMarketService, OnetLaborMarketService>();
builder.Services.AddScoped<IResumeTextExtractionService, ResumeTextExtractionService>();
builder.Services.AddSingleton<IResumeUploadValidator, ResumeUploadValidator>();
builder.Services.AddScoped<IProfessionalProfileExtractionService, NemotronProfileExtractionService>();
builder.Services.AddScoped<IPhysicalAssetDetectionService, NvidiaAssetDetectionService>();
// Temporary in-memory store; #8 (FC1) swaps this for the EF Core/PostgreSQL implementation.
builder.Services.AddSingleton<IFinancialEntryStore, InMemoryFinancialEntryStore>();
builder.Services.AddSingleton<IImageUploadValidator, ImageUploadValidator>();
builder.Services.AddSingleton<IPossessionImageStorage, FilesystemPossessionImageStorage>();
builder.Services.AddScoped<IAssetValuationService, AiEstimatedValuationService>();
builder.Services.AddScoped<IOccupationMatchingService, OnetOccupationMatchingService>();
builder.Services.AddScoped<ICompensationEstimationService, AiEstimatedCompensationService>();
builder.Services.AddScoped<IProfessionalProfileRepository, EfProfessionalProfileRepository>();
builder.Services.AddScoped<IPhysicalAssetRepository, EfPhysicalAssetRepository>();

// setup connection to postgresql database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");
}
else
{
    Console.WriteLine("Default Connection Configured.");
}

builder.Services.AddDbContext<MoneyMirrorDbContext>(options =>
    options.UseNpgsql(connectionString));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.MapGet("/api/possession-images/{reference}", async (string reference, IPossessionImageStorage storage) =>
{
    var stream = await storage.OpenReadAsync(reference);
    return stream is null
        ? Results.NotFound()
        : Results.File(stream, PossessionImageContentType.FromFileName(reference));
});

var shouldApplyMigrations = app.Environment.IsDevelopment()
    || builder.Configuration.GetValue<bool>("Database:ApplyMigrations");

if (shouldApplyMigrations)
{
    // automatically apply migrations
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider
        .GetRequiredService<MoneyMirrorDbContext>();

    await db.Database.MigrateAsync();

    if (app.Environment.IsDevelopment())
    {
        // Dev-only smoke test for O*NET wiring (#181). It calls a stable,
        // real occupation record and deliberately does no matching/ranking.
        app.MapGet("/dev/onet-smoke-test", async (IOnetOccupationDataService onet) =>
        {
            try
            {
                var occupation = await onet.GetOccupationAsync("15-1252.00");
                return Results.Ok(new { occupation.Code, occupation.Title });
            }
            catch (OnetOccupationDataException ex)
            {
                return Results.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway);
            }
        });

        // Dev-only smoke test for the F3 AI seam (#44) - proves ILlmService and
        // IVisionService round-trip against real providers. No feature logic.
        app.MapGet("/dev/ai-smoke-test", async (ILlmService llm, IVisionService vision) =>
        {
            var results = new Dictionary<string, string>();

            try
            {
                results["llm"] = await llm.CompleteAsync("Reply with exactly the word: pong");
            }
            catch (LlmServiceException ex)
            {
                results["llmError"] = ex.Message;
            }

            try
            {
                // 1x1 white pixel PNG - just enough to prove the call round-trips.
                var pixel = Convert.FromBase64String(
                    "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
                results["vision"] = await vision.DetectAsync(
                    pixel, "image/png", "Describe this image in one short sentence.");
            }
            catch (VisionServiceException ex)
            {
                results["visionError"] = ex.Message;
            }

            return Results.Ok(results);
        });
    }
}

app.Run();
