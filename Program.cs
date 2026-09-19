using PittMoney.Ai;
using PittMoney.Ai.Configuration;
using PittMoney.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.Configure<NemotronOptions>(
    builder.Configuration.GetSection(NemotronOptions.SectionName));
builder.Services.Configure<VisionModelOptions>(
    builder.Configuration.GetSection(VisionModelOptions.SectionName));

builder.Services.AddHttpClient<ILlmService, NemotronLlmService>();
builder.Services.AddHttpClient<IVisionService, NvidiaVisionService>();

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
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
