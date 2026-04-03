using Atlas.Application;
using Atlas.Infrastructure;
using Atlas.PlatformHost;
using Atlas.Core.Identity;
using Atlas.Core.Tenancy;
using FluentValidation.AspNetCore;
using Hangfire;
using Hangfire.Storage.SQLite;
using NLog.Web;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);
var legacyConfigRoot = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "Atlas.WebApi"));

builder.Configuration
    .AddJsonFile(Path.Combine(legacyConfigRoot, "appsettings.json"), optional: true, reloadOnChange: false)
    .AddJsonFile(
        Path.Combine(legacyConfigRoot, $"appsettings.{builder.Environment.EnvironmentName}.json"),
        optional: true,
        reloadOnChange: false);
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["Database:ConnectionString"] = $"Data Source={Path.Combine(legacyConfigRoot, "atlas.db")}"
});

if (builder.Environment.IsDevelopment())
{
    builder.WebHost.UseUrls("http://localhost:5001");
}

builder.Logging.ClearProviders();
builder.Host.UseNLog();

builder.Services.AddControllers();
builder.Services.AddHealthChecks();
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.AddCors(options =>
{
    options.AddPolicy("PlatformHostCors", policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
builder.Services.AddLocalization();
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddOpenApiDocument(config =>
{
    config.Title = "Atlas Platform Host API";
    config.Version = "v1";
    config.Description = "Atlas PlatformHost skeleton for PR-1.";
});
builder.Services.AddReverseProxy();
builder.Services.AddHangfire(config =>
    config.UseSQLiteStorage("hangfire-platformhost.db", new SQLiteStorageOptions
    {
        JournalMode = SQLiteStorageOptions.JournalModes.WAL
    }));
builder.Services.AddAtlasApplicationShared()
    .AddAtlasApplicationPlatform();
builder.Services.AddAtlasInfrastructureShared(builder.Configuration)
    .AddAtlasInfrastructurePlatform(builder.Configuration);
builder.Services.AddScoped<ITenantProvider>(_ => new TenantContext(TenantId.Empty));
builder.Services.AddScoped<ICurrentUserAccessor, PlatformHostCurrentUserAccessor>();
builder.Services.AddScoped<IAppContextAccessor, PlatformHostAppContextAccessor>();
builder.Services.AddScoped<IProjectContextAccessor, PlatformHostProjectContextAccessor>();
builder.Services.AddScoped<IClientContextAccessor, PlatformHostClientContextAccessor>();

var serviceName = "Atlas.PlatformHost";
var serviceVersion = typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0";
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(serviceName, serviceVersion: serviceVersion))
    .WithTracing(tracing =>
    {
        tracing.AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation();
    })
    .WithMetrics(metrics =>
    {
        metrics.AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation();
    });

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseOpenApi();
    app.UseSwaggerUi();
}

app.UseRequestLocalization(options =>
{
    var supportedCultures = new[] { "zh-CN", "en-US" };
    options.SetDefaultCulture("zh-CN")
        .AddSupportedCultures(supportedCultures)
        .AddSupportedUICultures(supportedCultures);
    options.ApplyCurrentCultureToResponseHeaders = true;
});
app.UseCors("PlatformHostCors");
app.UseRouting();
app.MapHealthChecks("/internal/health/live");
app.MapHealthChecks("/internal/health/ready");
app.MapControllers();
app.MapGet("/", () => Results.Ok(new
{
    host = "PlatformHost",
    status = "running",
    environment = app.Environment.EnvironmentName
}));

app.Run();
