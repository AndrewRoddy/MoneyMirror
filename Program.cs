using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MoneyMirror.Ai;
using MoneyMirror.Ai.Configuration;
using MoneyMirror.Components;
using MoneyMirror.Data;
using MoneyMirror.Features.Financial;
using MoneyMirror.HumanCapital;
using MoneyMirror.HumanCapital.Configuration;
using MoneyMirror.PhysicalAssets;

// Containers start with no LANG/LC_ALL, so .NET falls back to the invariant culture
// and renders currency as "¤" instead of "$". Pin the formatting culture so money
// looks the same in Docker as it does on a dev machine.
var appCulture = new CultureInfo("en-US");
CultureInfo.DefaultThreadCurrentCulture = appCulture;
CultureInfo.DefaultThreadCurrentUICulture = appCulture;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.Configure<Microsoft.AspNetCore.SignalR.HubOptions>(options =>
{
    options.MaximumReceiveMessageSize = 512 * 1024;
});

builder.Services.Configure<NemotronOptions>(
    builder.Configuration.GetSection(NemotronOptions.SectionName)
);
builder.Services.Configure<BlsOptions>(builder.Configuration.GetSection(BlsOptions.SectionName));
builder.Services.Configure<VisionModelOptions>(
    builder.Configuration.GetSection(VisionModelOptions.SectionName)
);
builder.Services.Configure<ResumeUploadOptions>(
    builder.Configuration.GetSection(ResumeUploadOptions.SectionName)
);
builder.Services.Configure<ImageUploadOptions>(
    builder.Configuration.GetSection(ImageUploadOptions.SectionName)
);

builder.Services.AddTransient<TransientFaultRetryHandler>();
builder.Services.AddMemoryCache();

// Both AI clients share NVIDIA's endpoint, which sheds load with a 503 when its
// workers are saturated - see TransientFaultRetryHandler. HttpClient.Timeout
// wraps the whole SendAsync pipeline, including TransientFaultRetryHandler's
// retries, so this is a hard ceiling on total time spent per call, not just
// the first attempt. #261: previously unset (100s .NET default), so a stuck
// request had no clear bound and no chance to surface the app's own
// "could not be loaded" error UI. 90s for the LLM because Nemotron is a
// reasoning model that has been observed taking upwards of 60s for a
// legitimate (non-error) response; the vision model and the plain BLS REST
// API are comparatively fast, so they get tighter budgets.
builder
    .Services.AddHttpClient<ILlmService, NemotronLlmService>(client =>
        client.Timeout = TimeSpan.FromSeconds(90)
    )
    .AddHttpMessageHandler<TransientFaultRetryHandler>();
builder
    .Services.AddHttpClient<IVisionService, NvidiaVisionService>(client =>
        client.Timeout = TimeSpan.FromSeconds(60)
    )
    .AddHttpMessageHandler<TransientFaultRetryHandler>();
builder.Services.AddHttpClient<IBlsWageDataService, BlsWageDataService>(client =>
    client.Timeout = TimeSpan.FromSeconds(15)
);
builder.Services.AddScoped<
    IMarketPotentialExplanationService,
    NemotronMarketPotentialExplanationService
>();
builder.Services.AddScoped<IMarketPotentialPipeline, MarketPotentialPipeline>();
builder.Services.AddScoped<IResumeTextExtractionService, ResumeTextExtractionService>();
builder.Services.AddSingleton<IResumeUploadValidator, ResumeUploadValidator>();
builder.Services.AddScoped<
    IProfessionalProfileExtractionService,
    NemotronProfileExtractionService
>();
builder.Services.AddScoped<IPhysicalAssetDetectionService, NvidiaAssetDetectionService>();
builder.Services.AddScoped<ISamSegmentationEngine, BlazorSamSegmentationEngine>();

// Temporary in-memory store; #8 (FC1) swaps this for the EF Core/PostgreSQL implementation.
builder.Services.AddSingleton<IFinancialEntryStore, InMemoryFinancialEntryStore>();
builder.Services.AddSingleton<IImageUploadValidator, ImageUploadValidator>();
builder.Services.AddSingleton<IPossessionImageStorage, FilesystemPossessionImageStorage>();
builder.Services.AddScoped<IAssetValuationService, AiEstimatedValuationService>();
builder.Services.AddScoped<IOccupationMatchingService, AiSuggestedOccupationMatchingService>();
builder.Services.AddScoped<ICompensationEstimationService, AiEstimatedCompensationService>();
builder.Services.AddScoped<IMarketPotentialSummaryService, CachedMarketPotentialSummaryService>();
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

builder.Services.AddDbContext<MoneyMirrorDbContext>(options => options.UseNpgsql(connectionString));

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

app.MapGet(
    "/api/possession-images/{reference}",
    async (string reference, IPossessionImageStorage storage) =>
    {
        var stream = await storage.OpenReadAsync(reference);
        return stream is null
            ? Results.NotFound()
            : Results.File(stream, PossessionImageContentType.FromFileName(reference));
    }
);

var shouldApplyMigrations =
    app.Environment.IsDevelopment()
    || builder.Configuration.GetValue<bool>("Database:ApplyMigrations");

if (shouldApplyMigrations)
{
    // automatically apply migrations
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<MoneyMirrorDbContext>();

    await db.Database.MigrateAsync();

    if (app.Environment.IsDevelopment())
    {
        // Dev-only smoke test for the F3 AI seam (#44) - proves ILlmService and
        // IVisionService round-trip against real providers. No feature logic.
        app.MapGet(
            "/dev/ai-smoke-test",
            async (ILlmService llm, IVisionService vision) =>
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
                        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII="
                    );
                    results["vision"] = await vision.DetectAsync(
                        pixel,
                        "image/png",
                        "Describe this image in one short sentence."
                    );
                }
                catch (VisionServiceException ex)
                {
                    results["visionError"] = ex.Message;
                }

                return Results.Ok(results);
            }
        );
    }
}

app.Run();
