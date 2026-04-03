using Atlas.Application;
using FluentValidation.AspNetCore;
using NLog.Web;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    builder.WebHost.UseUrls("http://localhost:5001");
}

builder.Logging.ClearProviders();
builder.Host.UseNLog();

builder.Services.AddControllers();
builder.Services.AddHealthChecks();
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
builder.Services.AddAtlasApplication();

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
