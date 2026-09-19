using MoneyMirror.Ai;
using MoneyMirror.Ai.Configuration;
using MoneyMirror.Components;
using MoneyMirror.HumanCapital;
using MoneyMirror.PhysicalAssets;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

builder.Services.Configure<NemotronOptions>(
    builder.Configuration.GetSection(NemotronOptions.SectionName)
);
builder.Services.Configure<VisionModelOptions>(
    builder.Configuration.GetSection(VisionModelOptions.SectionName));
builder.Services.Configure<ResumeUploadOptions>(
    builder.Configuration.GetSection(ResumeUploadOptions.SectionName));
builder.Services.Configure<ImageUploadOptions>(
    builder.Configuration.GetSection(ImageUploadOptions.SectionName));

builder.Services.AddHttpClient<ILlmService, NemotronLlmService>();
builder.Services.AddHttpClient<IVisionService, NvidiaVisionService>();
builder.Services.AddScoped<IResumeTextExtractionService, ResumeTextExtractionService>();
builder.Services.AddSingleton<IResumeUploadValidator, ResumeUploadValidator>();
builder.Services.AddScoped<IProfessionalProfileExtractionService, NemotronProfileExtractionService>();
builder.Services.AddScoped<IPhysicalAssetDetectionService, NvidiaAssetDetectionService>();
builder.Services.AddSingleton<IImageUploadValidator, ImageUploadValidator>();
builder.Services.AddSingleton<IPossessionImageStorage, FilesystemPossessionImageStorage>();
builder.Services.AddScoped<IAssetValuationService, AiEstimatedValuationService>();

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

if (app.Environment.IsDevelopment())
{
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

app.Run();
