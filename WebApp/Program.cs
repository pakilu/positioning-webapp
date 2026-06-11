using App.BLL.Positioning;
using App.DAL.EF;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using WebApp.Hubs;
using WebApp.Models.Mqtt;
using WebApp.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ??
                       throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
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

// --- Positioning pipeline ---------------------------------------------------
// Pure math; singleton.
builder.Services.AddSingleton<ITrilaterationSolver, LeastSquaresTrilaterationSolver>();

// Caches anchor coordinates per session; uses IServiceScopeFactory for DB access.
builder.Services.AddSingleton<IAnchorPositionProvider, AnchorPositionProvider>();

// Rolling in-memory cache of the latest distance per (session, tag, anchor).
builder.Services.AddSingleton<IMeasurementBuffer, InMemoryMeasurementBuffer>();

// SignalR sink for computed PositionResults.
builder.Services.AddSingleton<IPositionResultPublisher, SignalRPositionResultPublisher>();

// Pipeline options (could later be bound from configuration).
builder.Services.AddSingleton(new PositioningPipelineOptions());

// TimeProvider is registered by the framework, but make sure it's there.
builder.Services.AddSingleton(TimeProvider.System);

// The orchestrator itself.
builder.Services.AddSingleton<IPositioningPipeline, PositioningPipeline>();
// ---------------------------------------------------------------------------

// Background service that subscribes to Mosquitto and re-broadcasts via SignalR.
builder.Services.AddHostedService<MqttIngestService>();
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
