using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Atlas.AppHost;
using Atlas.AppHost.Sdk.Health;
using Atlas.AppHost.Sdk.Hosting;
using Atlas.Application;
using Atlas.Application.Options;
using Atlas.Core.Identity;
using Atlas.Core.Tenancy;
using Atlas.Infrastructure;
using Atlas.WorkflowCore;
using Atlas.WorkflowCore.DSL;
using FluentValidation.AspNetCore;
using Hangfire;
using Hangfire.Storage.SQLite;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
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
    builder.WebHost.UseUrls("http://localhost:5002");
}

builder.Logging.ClearProviders();
builder.Host.UseNLog();

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<SecurityOptions>(builder.Configuration.GetSection("Security"));
builder.Services.AddControllers();
builder.Services.AddHealthChecks();
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.AddLocalization();
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddAtlasAppHostSupport(builder.Configuration);
builder.Services.AddAtlasApplicationShared()
    .AddAtlasApplicationPlatform()
    .AddAtlasApplicationRuntime();
builder.Services.AddAtlasInfrastructureShared(builder.Configuration)
    .AddAtlasInfrastructurePlatform(builder.Configuration)
    .AddAtlasInfrastructureAppRuntime(builder.Configuration);
builder.Services.AddScoped<ITenantProvider, AppHostTenantProvider>();
builder.Services.AddScoped<ICurrentUserAccessor, AppHostCurrentUserAccessor>();
builder.Services.AddScoped<IClientContextAccessor, AppHostClientContextAccessor>();
builder.Services.AddScoped<IAppContextAccessor, AppHostAppContextAccessor>();
builder.Services.AddScoped<IProjectContextAccessor, AppHostProjectContextAccessor>();

var securityOptions = builder.Configuration.GetSection("Security").Get<SecurityOptions>() ?? new SecurityOptions();
var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = securityOptions.EnforceHttps && !builder.Environment.IsDevelopment();
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            NameClaimType = JwtRegisteredClaimNames.Sub,
            RoleClaimType = ClaimTypes.Role,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Cookies["access_token"];
                if (!string.IsNullOrEmpty(accessToken))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddHangfire(config =>
    config.UseSQLiteStorage("hangfire-apphost.db", new SQLiteStorageOptions
    {
        JournalMode = SQLiteStorageOptions.JournalModes.WAL
    }));
builder.Services.AddWorkflowCore();
builder.Services.AddWorkflowCoreDsl(options =>
{
    options.AddNamespace("Atlas.WorkflowCore.Primitives");
});
builder.Services.AddHostedService<Atlas.Infrastructure.Services.WorkflowHostedService>();
builder.Services.AddOpenApiDocument(config =>
{
    config.Title = "Atlas App Host API";
    config.Version = "v1";
    config.Description = "Atlas AppHost runtime data plane.";
});

var serviceName = "Atlas.AppHost";
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
var instanceConfigLoader = app.Services.GetRequiredService<AppInstanceConfigurationLoader>();
var instanceConfig = instanceConfigLoader.Load();
var version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0";

if (app.Environment.IsDevelopment())
{
    app.UseOpenApi();
    app.UseSwaggerUi();
}

app.UseAtlasAppHostDefaults();
app.UseAuthentication();
app.UseAuthorization();
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
