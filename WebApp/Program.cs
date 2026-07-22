using System.Text.RegularExpressions;
using App.BLL.Positioning;
using App.DAL.EF;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using WebApp.Hubs;
using WebApp.Models.Mqtt;
using WebApp.Models.Serial;
using WebApp.Services;

var builder = WebApplication.CreateBuilder(args);

// Load .env file (if present) into process environment variables so that
// sensitive values like DB credentials can be kept out of appsettings.json
// and out of source control.
LoadDotEnv(Path.Combine(builder.Environment.ContentRootPath, ".env"));

var rawConnectionString = builder.Configuration.GetConnectionString("DefaultConnection") ??
                          throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// Substitute ${VAR} placeholders in the connection string with environment
// variable values (loaded from .env above, or from the real environment).
var connectionString = Regex.Replace(rawConnectionString, @"\$\{([^}]+)\}", match =>
{
    var name = match.Groups[1].Value;
    return Environment.GetEnvironmentVariable(name)
           ?? throw new InvalidOperationException(
               $"Environment variable '{name}' referenced by ConnectionStrings:DefaultConnection is not set. " +
               $"Define it in WebApp/.env or in the process environment.");
});
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(
            connectionString,
            o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
        .ConfigureWarnings(w =>
            w.Throw(RelationalEventId.MultipleCollectionIncludeWarning))
        .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTrackingWithIdentityResolution);

    if (!builder.Environment.IsProduction())
    {
        options.EnableDetailedErrors()
            .EnableSensitiveDataLogging();
    }
});
builder.Services.AddDatabaseDeveloperPageExceptionFilter();



// Add services to the container.
builder.Services.AddControllersWithViews();

// --- Real-time positioning pipeline -----------------------------------------
// SignalR provides the WebSocket endpoint that browser clients connect to.
builder.Services.AddSignalR();

// MQTT settings bound from appsettings.json ("Mqtt" section).
builder.Services.Configure<MqttOptions>(builder.Configuration.GetSection(MqttOptions.SectionName));
builder.Services.Configure<SerialOptions>(builder.Configuration.GetSection(SerialOptions.SectionName));

// --- Positioning pipeline ---------------------------------------------------
// Pure math; singleton.
builder.Services.AddSingleton<ITrilaterationSolver, LeastSquaresTrilaterationSolver>();

// Caches anchor coordinates per session; uses IServiceScopeFactory for DB access.
builder.Services.AddSingleton<IAnchorPositionProvider, AnchorPositionProvider>();

// Rolling in-memory cache of the latest distance per (session, tag, anchor).
builder.Services.AddSingleton<IMeasurementBuffer, InMemoryMeasurementBuffer>();

// SignalR sink for computed PositionResults.
builder.Services.AddSingleton<IPositionResultPublisher, SignalRPositionResultPublisher>();

// Pipeline options, bound from the "Positioning" section of appsettings.json
// (falls back to the defaults on PositioningPipelineOptions when absent).
var positioningOptions = new PositioningPipelineOptions();
builder.Configuration.GetSection("Positioning").Bind(positioningOptions);
builder.Services.AddSingleton(positioningOptions);

// TimeProvider is registered by the framework, but make sure it's there.
builder.Services.AddSingleton(TimeProvider.System);

// The orchestrator itself.
builder.Services.AddSingleton<IPositioningPipeline, PositioningPipeline>();
builder.Services.AddSingleton<IngestProcessor>();
// ---------------------------------------------------------------------------

// Background service that subscribes to Mosquitto and re-broadcasts via SignalR.
// Registered as a singleton so it can also serve as IAnchorListPublisher
// (SessionsController publishes retained anchor lists through the same
// managed MQTT client on session lifecycle events).
builder.Services.AddSingleton<MqttIngestService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<MqttIngestService>());
builder.Services.AddSingleton<IAnchorListPublisher>(sp => sp.GetRequiredService<MqttIngestService>());
builder.Services.AddHostedService<SerialIngestService>();
// ---------------------------------------------------------------------------

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

// WebSocket endpoint for live positioning data.
app.MapHub<PositioningHub>("/hubs/positioning");


app.Run();


static void LoadDotEnv(string path)
{
    if (!File.Exists(path))
    {
        return;
    }

    foreach (var rawLine in File.ReadAllLines(path))
    {
        var line = rawLine.Trim();
        if (line.Length == 0 || line.StartsWith('#'))
        {
            continue;
        }

        // Support an optional leading `export ` for shell-compatibility.
        if (line.StartsWith("export ", StringComparison.Ordinal))
        {
            line = line["export ".Length..].TrimStart();
        }

        var eq = line.IndexOf('=');
        if (eq <= 0)
        {
            continue;
        }

        var key = line[..eq].Trim();
        var value = line[(eq + 1)..].Trim();

        // Strip a single pair of surrounding quotes if present.
        if (value.Length >= 2 &&
            ((value[0] == '"' && value[^1] == '"') ||
             (value[0] == '\'' && value[^1] == '\'')))
        {
            value = value[1..^1];
        }

        // Do not overwrite variables that are already set in the real
        // environment (which allows overriding .env in production/CI).
        if (Environment.GetEnvironmentVariable(key) is null)
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }
}
