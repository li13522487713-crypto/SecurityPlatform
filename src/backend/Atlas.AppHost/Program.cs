using System.IdentityModel.Tokens.Jwt;
using System.IO.Compression;
using System.Security.Claims;
using System.Text;
using Atlas.AppHost;
using Atlas.AppHost.Sdk.Health;
using Atlas.AppHost.Sdk.Hosting;
using Atlas.Application;
using Atlas.Application.Alert.Mappings;
using Atlas.Application.Approval.Mappings;
using Atlas.Application.Assets.Mappings;
using Atlas.Application.Options;
using Atlas.Application.Resources;
using Atlas.Core.Identity;
using Atlas.Core.Tenancy;
using Atlas.Infrastructure;
using Atlas.Presentation.Shared.Filters;
using Atlas.Presentation.Shared.Mappings;
using Atlas.Presentation.Shared.Middlewares;
using Atlas.WorkflowCore;
using Atlas.WorkflowCore.DSL;
using FluentValidation;
using FluentValidation.AspNetCore;
using Hangfire;
using Hangfire.Storage.SQLite;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.ResponseCompression;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.Extensions.Localization;
using Microsoft.IdentityModel.Tokens;
using NLog.Web;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// 平台配置根目录：支持通过环境变量 ATLAS_PLATFORM_CONFIG_ROOT 覆盖，
// 默认值兼容开发环境的相对目录布局。
var platformConfigRoot = Environment.GetEnvironmentVariable("ATLAS_PLATFORM_CONFIG_ROOT");
if (string.IsNullOrWhiteSpace(platformConfigRoot))
{
    platformConfigRoot = builder.Configuration["Atlas:Runtime:PlatformConfigRoot"];
}
if (string.IsNullOrWhiteSpace(platformConfigRoot))
{
    platformConfigRoot = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "Atlas.PlatformHost"));
}

builder.Configuration
    .AddJsonFile(Path.Combine(platformConfigRoot, "appsettings.json"), optional: true, reloadOnChange: false)
    .AddJsonFile(
        Path.Combine(platformConfigRoot, $"appsettings.{builder.Environment.EnvironmentName}.json"),
        optional: true,
        reloadOnChange: false);

// 加载平台 setup 完成后持久化的运行时配置（如有）
var platformRuntimeConfigPath = Path.Combine(platformConfigRoot, "appsettings.runtime.json");
builder.Configuration.AddJsonFile(platformRuntimeConfigPath, optional: true, reloadOnChange: false);

// 也加载 AppHost 自身的运行时配置（优先级更高，允许独立覆盖）
var appRuntimeConfigPath = Path.Combine(builder.Environment.ContentRootPath, "appsettings.runtime.json");
builder.Configuration.AddJsonFile(appRuntimeConfigPath, optional: true, reloadOnChange: false);

var platformSetupStatePath = Path.Combine(platformConfigRoot, "setup-state.json");
var platformSetupReadyForRegistration = File.Exists(platformSetupStatePath) &&
    File.ReadAllText(platformSetupStatePath).Contains("\"Ready\"", StringComparison.OrdinalIgnoreCase);
var appSetupStatePath = Path.Combine(builder.Environment.ContentRootPath, "app-setup-state.json");
var appSetupReadyForRegistration = File.Exists(appSetupStatePath) &&
    File.ReadAllText(appSetupStatePath).Contains("\"Ready\"", StringComparison.OrdinalIgnoreCase);
var runtimeReadyForRegistration = platformSetupReadyForRegistration && appSetupReadyForRegistration;

// 仅当运行时配置未提供数据库连接串时才使用默认路径
if (string.IsNullOrWhiteSpace(builder.Configuration["Database:ConnectionString"]))
{
    var dbPath = Path.Combine(platformConfigRoot, "atlas.db");
    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Database:ConnectionString"] = $"Data Source={dbPath}"
    });
}

builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["Setup:StateFilePath"] = platformSetupStatePath
});

if (builder.Environment.IsDevelopment()
    && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
{
    builder.WebHost.UseUrls("http://localhost:5002");
}

builder.Logging.ClearProviders();
builder.Host.UseNLog();

// ─── Options ───
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<SecurityOptions>(builder.Configuration.GetSection("Security"));
builder.Services.Configure<XssOptions>(builder.Configuration.GetSection("Xss"));
builder.Services.Configure<FileStorageOptions>(builder.Configuration.GetSection("FileStorage"));
builder.Services.Configure<PasswordPolicyOptions>(builder.Configuration.GetSection("Security:PasswordPolicy"));
builder.Services.Configure<LockoutPolicyOptions>(builder.Configuration.GetSection("Security:LockoutPolicy"));
builder.Services.Configure<BootstrapAdminOptions>(builder.Configuration.GetSection("Security:BootstrapAdmin"));
builder.Services.Configure<ApprovalSeedDataOptions>(builder.Configuration.GetSection("Approval:SeedData"));
builder.Services.Configure<Atlas.Presentation.Shared.Tenancy.TenancyOptions>(builder.Configuration.GetSection("Tenancy"));
builder.Services.Configure<IdempotencyOptions>(builder.Configuration.GetSection("Idempotency"));
builder.Services.Configure<TableViewDefaultOptions>(builder.Configuration.GetSection("TableViewDefaults"));
builder.Services.Configure<Atlas.Presentation.Shared.Identity.AppOptions>(builder.Configuration.GetSection("App"));
builder.Services.Configure<Atlas.Application.Options.DatabaseInitializerOptions>(builder.Configuration.GetSection("DatabaseInitializer"));

// ─── Controllers ───
var mvcBuilder = builder.Services.AddControllers(options =>
{
    options.Filters.Add<IdempotencyFilter>();
})
.AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new Atlas.Presentation.Shared.Json.FlexibleLongJsonConverter());
    options.JsonSerializerOptions.Converters.Add(new Atlas.Presentation.Shared.Json.FlexibleNullableLongJsonConverter());
    options.JsonSerializerOptions.Converters.Add(new Atlas.Presentation.Shared.Json.SensitiveObjectConverterFactory());
});

builder.Services.AddOpenApiDocument(config =>
{
    config.Title = "Atlas App Host API";
    config.Version = "v1";
    config.Description = "Atlas AppHost — 应用运行时数据面 API";
    config.UseControllerSummaryAsTagDescription = true;
});

// ─── Health, Cache, HttpContext ───
builder.Services.AddHealthChecks();
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();

// ─── CORS ───
builder.Services.AddCors(options =>
{
    options.AddPolicy("AppHostCors", policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? Array.Empty<string>();
        if (origins.Length > 0)
        {
            policy.WithOrigins(origins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
        else
        {
            policy.AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    });
});

// ─── Localization ───
builder.Services.AddLocalization(opts => opts.ResourcesPath = "");
builder.Services.Configure<Microsoft.AspNetCore.Builder.RequestLocalizationOptions>(opts =>
{
    var supportedCultures = new[] { "zh-CN", "en-US" };
    opts.SetDefaultCulture("zh-CN")
        .AddSupportedCultures(supportedCultures)
        .AddSupportedUICultures(supportedCultures);
    opts.ApplyCurrentCultureToResponseHeaders = true;
});

// ─── FluentValidation ───
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblies([
    typeof(Atlas.Application.Validators.AuthTokenRequestValidator).Assembly,
    typeof(Atlas.Application.Approval.Validators.ApprovalFlowDefinitionCreateRequestValidator).Assembly,
    typeof(Atlas.Application.Assets.Validators.AssetValidator).Assembly,
    typeof(Atlas.Application.Alert.Validators.AlertRecordValidator).Assembly,
    typeof(Atlas.Application.Audit.Validators.AuditRecordValidator).Assembly,
    typeof(Atlas.Application.AgentTeam.Validators.AgentTeamCreateRequestValidator).Assembly,
    typeof(Atlas.Application.Workflow.Validators.PublishEventRequestValidator).Assembly,
    typeof(Atlas.Presentation.Shared.Validators.ChangePasswordViewModelValidator).Assembly,
]);

// ─── AppHost SDK ───
builder.Services.AddAtlasAppHostSupport(builder.Configuration);

// ─── Application + Infrastructure ───
var appServices = builder.Services.AddAtlasApplicationShared(
    typeof(AlertMappingProfile).Assembly,
    typeof(ApprovalMappingProfile).Assembly,
    typeof(AssetsMappingProfile).Assembly,
    typeof(Atlas.Application.LogicFlow.Flows.Mappings.LogicFlowMappingProfile).Assembly,
    typeof(Atlas.Application.BatchProcess.Mappings.BatchProcessMappingProfile).Assembly,
    typeof(WebApiMappingProfile).Assembly)
    .AddAtlasApplicationPlatform();
if (runtimeReadyForRegistration)
{
    appServices.AddAtlasApplicationRuntime();
    builder.Services.AddAtlasInfrastructureShared(builder.Configuration)
        .AddAtlasInfrastructurePlatform(builder.Configuration)
        .AddAtlasInfrastructureAppRuntime(builder.Configuration);
}
else
{
    builder.Services.AddAtlasInfrastructureShared(builder.Configuration);
}

// ─── DI：AppHost-specific context accessors ───
builder.Services.AddScoped<ITenantProvider, AppHostTenantProvider>();
builder.Services.AddScoped<ICurrentUserAccessor, AppHostCurrentUserAccessor>();
builder.Services.AddScoped<IClientContextAccessor, AppHostClientContextAccessor>();
builder.Services.AddScoped<IAppContextAccessor, AppHostAppContextAccessor>();
builder.Services.AddScoped<IProjectContextAccessor, AppHostProjectContextAccessor>();
builder.Services.AddScoped<IdempotencyFilter>();

// ─── Antiforgery ───
var securityOptions = builder.Configuration.GetSection("Security").Get<SecurityOptions>() ?? new SecurityOptions();
var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "XSRF-TOKEN";
    options.Cookie.HttpOnly = false;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = securityOptions.EnforceHttps && !builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.Always
        : CookieSecurePolicy.None;
});

// ─── Authentication: JWT (with cookie fallback) ───
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
builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new AuthorizationPolicyBuilder(
            JwtBearerDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser()
        .Build();
});
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, ApiAuthorizationMiddlewareResultHandler>();
builder.Services.AddSingleton<Atlas.Presentation.Shared.Services.MigrationGovernanceMetricsStore>();

// ─── Response Compression ───
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(["application/json"]);
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});
builder.Services.Configure<BrotliCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);

// ─── Hangfire (client only, no server in AppHost; gated by setup state) ───
if (runtimeReadyForRegistration)
{
    builder.Services.AddHangfire(config =>
        config.UseSQLiteStorage("hangfire-apphost.db", new SQLiteStorageOptions
        {
            JournalMode = SQLiteStorageOptions.JournalModes.WAL
        }));
}
else
{
    builder.Services.AddHangfire(config => { });
}

// ─── WorkflowCore ───
if (runtimeReadyForRegistration)
{
    builder.Services.AddWorkflowCore();
    builder.Services.AddWorkflowCoreDsl(options =>
    {
        options.AddNamespace("Atlas.WorkflowCore.Primitives");
    });
    builder.Services.AddHostedService<Atlas.Infrastructure.Services.WorkflowHostedService>();
}

// ─── OpenTelemetry ───
var serviceName = "Atlas.AppHost";
var serviceVersion = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "1.0.0";
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(serviceName, serviceVersion: serviceVersion))
    .WithTracing(tracing =>
    {
        tracing.AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation();
        var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            tracing.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
        }
        else if (builder.Environment.IsDevelopment()
            && builder.Configuration.GetValue<bool>("OpenTelemetry:EnableConsoleExporter"))
        {
            tracing.AddConsoleExporter();
        }
    })
    .WithMetrics(metrics =>
    {
        metrics.AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation();
        var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            metrics.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
        }
        else if (builder.Environment.IsDevelopment()
            && builder.Configuration.GetValue<bool>("OpenTelemetry:EnableConsoleExporter"))
        {
            metrics.AddConsoleExporter();
        }
    });

var app = builder.Build();
var instanceConfigLoader = app.Services.GetRequiredService<AppInstanceConfigurationLoader>();
var instanceConfig = instanceConfigLoader.Load();
var version = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "1.0.0";

// ─── Middleware pipeline ───
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<AppSetupModeMiddleware>();
app.UseMiddleware<XssProtectionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseOpenApi();
    app.UseSwaggerUi();
}

app.UseCors("AppHostCors");
app.UseResponseCompression();
app.UseRequestLocalization();
app.UseMiddleware<ClientContextMiddleware>();
app.UseRouting();
app.UseAuthentication();
app.UseMiddleware<AppContextMiddleware>();
app.UseMiddleware<AntiforgeryValidationMiddleware>();
app.UseMiddleware<TenantContextMiddleware>();
app.UseMiddleware<ApiVersionRewriteMiddleware>();
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

var startupLogo = """
    ___  _______ _        ___   _____
   / _ |/ __/ _ | |      / _ | / ___/
  / __ / _// __ | |__   / __ |/ /__
 /_/ |_/___/_/ |_|____/ /_/ |_|\___/
Atlas Security Platform
""";

app.Lifetime.ApplicationStarted.Register(() =>
{
    var addresses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var address in app.Urls)
    {
        if (!string.IsNullOrWhiteSpace(address))
        {
            addresses.Add(address);
        }
    }

    var server = app.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>();
    var serverAddresses = server.Features.Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>()?.Addresses;
    if (serverAddresses is not null)
    {
        foreach (var address in serverAddresses)
        {
            if (!string.IsNullOrWhiteSpace(address))
            {
                addresses.Add(address);
            }
        }
    }

    var orderedAddresses = addresses
        .OrderBy(static address => address, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    Console.WriteLine();
    Console.WriteLine(startupLogo);
    Console.WriteLine("Atlas AppHost 启动成功");
    Console.WriteLine($"环境: {app.Environment.EnvironmentName}");
    Console.WriteLine($"应用: {instanceConfig.AppKey} / 实例: {instanceConfig.InstanceId}");

    foreach (var address in orderedAddresses)
    {
        Console.WriteLine($"监听地址: {address}");
    }

    if (app.Environment.IsDevelopment())
    {
        foreach (var address in orderedAddresses)
        {
            Console.WriteLine($"Swagger: {address.TrimEnd('/')}/swagger");
        }
    }

    Console.WriteLine();
});

app.Run();
