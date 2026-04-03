using Atlas.AppHost.Sdk.Health;
using Atlas.AppHost.Sdk.Hosting;
using NLog.Web;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    builder.WebHost.UseUrls("http://localhost:5002");
}

builder.Logging.ClearProviders();
builder.Host.UseNLog();

builder.Services.AddControllers();
builder.Services.AddHealthChecks();
builder.Services.AddAtlasAppHostSupport(builder.Configuration);
builder.Services.AddOpenApiDocument(config =>
{
    config.Title = "Atlas App Host API";
    config.Version = "v1";
    config.Description = "Atlas AppHost skeleton for PR-1.";
});

var app = builder.Build();
var instanceConfigLoader = app.Services.GetRequiredService<AppInstanceConfigurationLoader>();
var instanceConfig = instanceConfigLoader.Load();
var version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0";

if (app.Environment.IsDevelopment())
{
    app.UseOpenApi();
    app.UseSwaggerUi();
}

app.UseAtlasAppHostDefaults();
app.MapHealthChecks("/internal/health/live");
app.MapHealthChecks("/internal/health/ready");
app.MapGet("/internal/health/live-report", () => Results.Ok(AppHealthResponseBuilder.BuildLive(version)));
app.MapGet("/internal/health/ready-report", () => Results.Ok(AppHealthResponseBuilder.BuildReady(version)));
app.MapGet("/", () => Results.Ok(new
{
    host = "AppHost",
    status = "running",
    appKey = instanceConfig.AppKey,
    instanceId = instanceConfig.InstanceId,
    environment = app.Environment.EnvironmentName
}));
app.MapControllers();

app.Run();
