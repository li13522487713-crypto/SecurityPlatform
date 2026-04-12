using AutoMapper;
using FluentValidation;
using FluentValidation.AspNetCore;
using Atlas.Application;
using Atlas.Application.Alert.Mappings;
using Atlas.Application.Approval.Mappings;
using Atlas.Application.Assets.Mappings;
using Atlas.Application.Options;
using Atlas.Application.Resources;
using Atlas.Infrastructure;
using Atlas.Presentation.Shared.Middlewares;
using Hangfire;
using Hangfire.Storage.SQLite;
using Atlas.Presentation.Shared.Tenancy;
using Atlas.WorkflowCore;
using Atlas.WorkflowCore.DSL;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Certificate;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using NLog.Web;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Atlas.Presentation.Shared.Authorization;
using Atlas.Presentation.Shared.Filters;
using Atlas.Presentation.Shared.Mappings;
using Atlas.Presentation.Shared.Security;
using Atlas.Application.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Infrastructure.Configuration;
using Atlas.Core.Setup;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.SignalR;
using Atlas.Core.Models;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Microsoft.Extensions.Localization;
using System.IO.Compression;
using Atlas.PlatformHost.ReverseProxy;
using Yarp.ReverseProxy.Configuration;

var builder = WebApplication.CreateBuilder(args);
var setupStateFilePath = builder.Configuration["Setup:StateFilePath"];
if (string.IsNullOrWhiteSpace(setupStateFilePath))
{
    setupStateFilePath = Path.Combine(builder.Environment.ContentRootPath, "setup-state.json");
}
// 运行时能力始终注册，setup 访问门禁由 SetupModeMiddleware 按当前状态动态控制。
builder.Services.AddSingleton(new PlatformRuntimeRegistrationMarker(true));

// ─── 配置来源 ───
// 优先加载 setup 完成后持久化的运行时配置（包含用户选定的数据库连接信息）
var runtimeConfigPath = Path.Combine(builder.Environment.ContentRootPath, "appsettings.runtime.json");
builder.Configuration.AddJsonFile(runtimeConfigPath, optional: true, reloadOnChange: false);

// 仅当 appsettings.runtime.json 未提供数据库配置时才使用默认 atlas.db
if (string.IsNullOrWhiteSpace(builder.Configuration["Database:ConnectionString"]))
{
    var dbPath = Path.Combine(builder.Environment.ContentRootPath, "atlas.db");
    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Database:ConnectionString"] = $"Data Source={dbPath}"
    });
}

DatabaseConfigurationSource? databaseConfigurationSource = null;
var dbConnectionString = builder.Configuration["Database:ConnectionString"];
var bootstrapTenantId = builder.Configuration["Security:BootstrapAdmin:TenantId"];
if (!string.IsNullOrWhiteSpace(dbConnectionString) && !string.IsNullOrWhiteSpace(bootstrapTenantId))
{
    var encryptionEnabled = builder.Configuration.GetValue<bool>("Database:Encryption:Enabled");
    var encryptionKey = builder.Configuration["Database:Encryption:Key"] ?? string.Empty;
    databaseConfigurationSource = new DatabaseConfigurationSource(
        dbConnectionString,
        bootstrapTenantId,
        encryptionEnabled,
        encryptionKey,
        setupStateFilePath);
    builder.Configuration.Sources.Add(databaseConfigurationSource);
}

var validateAutoMapperOnStartup = builder.Configuration.GetValue("AutoMapper:ValidateOnStartup", false);
var runHangfireServer = builder.Configuration.GetValue("Hangfire:RunServer", !builder.Environment.IsDevelopment());
var hangfireWorkerCount = builder.Configuration.GetValue("Hangfire:WorkerCount", builder.Environment.IsDevelopment() ? 1 : 3);
var hangfireQueuePollIntervalSeconds = builder.Configuration.GetValue("Hangfire:QueuePollIntervalSeconds", builder.Environment.IsDevelopment() ? 30 : 15);
var hangfireSchedulePollingIntervalSeconds = builder.Configuration.GetValue("Hangfire:SchedulePollingIntervalSeconds", builder.Environment.IsDevelopment() ? 60 : 15);
var hangfireJobExpirationCheckIntervalMinutes = builder.Configuration.GetValue("Hangfire:JobExpirationCheckIntervalMinutes", builder.Environment.IsDevelopment() ? 360 : 60);

if (builder.Environment.IsDevelopment()
    && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
{
    builder.WebHost.UseUrls("http://localhost:5001");
}

builder.Logging.ClearProviders();
builder.Host.UseNLog();

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
    config.Title = "Atlas Platform Host API";
    config.Version = "v1";
    config.Description = "Atlas PlatformHost — 平台控制面 API（含 YARP 反代到 AppHost）";
    config.UseControllerSummaryAsTagDescription = true;
});

// ─── Options ───
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<SecurityOptions>(builder.Configuration.GetSection("Security"));
builder.Services.Configure<XssOptions>(builder.Configuration.GetSection("Xss"));
builder.Services.Configure<FileStorageOptions>(builder.Configuration.GetSection("FileStorage"));
builder.Services.Configure<PasswordPolicyOptions>(builder.Configuration.GetSection("Security:PasswordPolicy"));
builder.Services.Configure<LockoutPolicyOptions>(builder.Configuration.GetSection("Security:LockoutPolicy"));
builder.Services.Configure<BootstrapAdminOptions>(builder.Configuration.GetSection("Security:BootstrapAdmin"));
builder.Services.Configure<ApprovalSeedDataOptions>(builder.Configuration.GetSection("Approval:SeedData"));
builder.Services.Configure<TenancyOptions>(builder.Configuration.GetSection("Tenancy"));
builder.Services.Configure<IdempotencyOptions>(builder.Configuration.GetSection("Idempotency"));
builder.Services.Configure<TableViewDefaultOptions>(builder.Configuration.GetSection("TableViewDefaults"));
builder.Services.Configure<Atlas.Presentation.Shared.Identity.AppOptions>(builder.Configuration.GetSection("App"));
builder.Services.Configure<Atlas.Application.Options.DatabaseInitializerOptions>(builder.Configuration.GetSection("DatabaseInitializer"));

// ─── OIDC/SSO ───
builder.Services.Configure<Atlas.Infrastructure.Security.OidcOptions>(builder.Configuration.GetSection("Oidc"));
builder.Services.AddScoped<Atlas.Infrastructure.Security.OidcAccountMapper>();
var oidcOptions = new Atlas.Infrastructure.Security.OidcOptions();
builder.Configuration.GetSection("Oidc").Bind(oidcOptions);
if (oidcOptions.Enabled)
{
    var effectiveProviders = oidcOptions.GetEffectiveProviders();
    var authBuilder = builder.Services.AddAuthentication();
    foreach (var provider in effectiveProviders)
    {
        var schemeName = $"oidc_{provider.ProviderId}";
        authBuilder.AddOpenIdConnect(schemeName, displayName: provider.DisplayName, options =>
        {
            options.Authority = provider.Authority;
            options.ClientId = provider.ClientId;
            options.ClientSecret = provider.ClientSecret;
            options.CallbackPath = string.IsNullOrWhiteSpace(provider.CallbackPath)
                ? $"/auth/sso/{provider.ProviderId}/callback"
                : provider.CallbackPath;
            options.ResponseType = "code";
            options.SaveTokens = false;
            foreach (var scope in provider.Scopes)
            {
                options.Scope.Add(scope);
            }
            options.Events.OnTokenValidated = async ctx =>
            {
                if (ctx.Principal is null) return;
                var mapper = ctx.HttpContext.RequestServices.GetRequiredService<Atlas.Infrastructure.Security.OidcAccountMapper>();
                var tenantProvider = ctx.HttpContext.RequestServices.GetRequiredService<ITenantProvider>();
                var tenantId = tenantProvider.GetTenantId();
                await mapper.MapOrCreateAsync(ctx.Principal, tenantId, provider.ProviderId, ctx.HttpContext.RequestAborted);
            };
        });
    }
}

// ─── CORS ───
builder.Services.AddCors(options =>
{
    options.AddPolicy("PlatformHostCors", policy =>
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

// ─── HttpClients ───
builder.Services.AddHttpClient("app-runtime-proxy", client =>
{
    var baseUrl = builder.Configuration["Atlas:Runtime:AppHostBaseUrl"] ?? "http://localhost:5002";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddSignalR();

// ─── HTTP Logging ───
builder.Services.AddHttpLogging(options =>
{
    options.LoggingFields = HttpLoggingFields.RequestPath
        | HttpLoggingFields.RequestMethod
        | HttpLoggingFields.RequestQuery
        | HttpLoggingFields.ResponseStatusCode
        | HttpLoggingFields.Duration;
});

// ─── OpenTelemetry ───
var serviceName = "Atlas.PlatformHost";
var serviceVersion = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "1.0.0";
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName: serviceName, serviceVersion: serviceVersion))
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
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
        metrics
            .AddAspNetCoreInstrumentation()
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

// ─── DI：HttpContext-based identity/tenant ───
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantProvider, HttpContextTenantProvider>();
builder.Services.AddScoped<Atlas.Core.Identity.ICurrentUserAccessor, Atlas.Presentation.Shared.Identity.HttpContextCurrentUserAccessor>();
builder.Services.AddScoped<Atlas.Core.Identity.IClientContextAccessor, Atlas.Presentation.Shared.Identity.HttpContextClientContextAccessor>();
builder.Services.AddScoped<Atlas.Core.Identity.IAppContextAccessor, Atlas.Presentation.Shared.Identity.HttpContextAppContextAccessor>();
builder.Services.AddScoped<Atlas.Core.Identity.IProjectContextAccessor, Atlas.Presentation.Shared.Identity.HttpContextProjectContextAccessor>();
builder.Services.AddScoped<IdempotencyFilter>();

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

// ─── Security validation (production) ───
var securityOptions = builder.Configuration.GetSection("Security").Get<SecurityOptions>() ?? new SecurityOptions();
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
var fileStorage = builder.Configuration.GetSection("FileStorage").Get<FileStorageOptions>() ?? new FileStorageOptions();
var storageProvider = fileStorage.Provider?.Trim().ToLowerInvariant() ?? FileStorageOptions.ProviderLocal;
if (!builder.Environment.IsDevelopment())
{
    var containsInsecurePlaceholder = (string value) =>
        string.IsNullOrWhiteSpace(value)
        || string.Equals(value, "PLACEHOLDER__SET_VIA_ENV_JWT_SIGNING_KEY", StringComparison.Ordinal)
        || string.Equals(value, FileStorageOptions.UnsafeDefaultSignedUrlSecret, StringComparison.Ordinal)
        || value.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase)
        || value.Contains("PLACEHOLDER__", StringComparison.OrdinalIgnoreCase);

    if (string.IsNullOrWhiteSpace(jwt.SigningKey)
        || containsInsecurePlaceholder(jwt.SigningKey)
        || jwt.SigningKey.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase)
        || jwt.SigningKey.Length < 32)
    {
        throw new InvalidOperationException("生产环境必须配置长度不少于32位的JWT SigningKey。");
    }

    if (string.IsNullOrWhiteSpace(fileStorage.SignedUrlSecret)
        || containsInsecurePlaceholder(fileStorage.SignedUrlSecret)
        || fileStorage.SignedUrlSecret.Length < 32)
    {
        throw new InvalidOperationException("生产环境必须配置长度不少于32位且非默认值的 FileStorage SignedUrlSecret。");
    }

    if (storageProvider == FileStorageOptions.ProviderMinio)
    {
        if (string.IsNullOrWhiteSpace(fileStorage.Minio.Endpoint)
            || string.IsNullOrWhiteSpace(fileStorage.Minio.AccessKey)
            || string.IsNullOrWhiteSpace(fileStorage.Minio.SecretKey)
            || string.IsNullOrWhiteSpace(fileStorage.Minio.BucketName))
        {
            throw new InvalidOperationException("生产环境启用 MinIO 存储时必须配置完整的 Endpoint/AccessKey/SecretKey/BucketName。");
        }
    }
    else if (storageProvider == FileStorageOptions.ProviderOss)
    {
        if (string.IsNullOrWhiteSpace(fileStorage.Oss.Endpoint)
            || string.IsNullOrWhiteSpace(fileStorage.Oss.AccessKeyId)
            || string.IsNullOrWhiteSpace(fileStorage.Oss.AccessKeySecret)
            || string.IsNullOrWhiteSpace(fileStorage.Oss.BucketName))
        {
            throw new InvalidOperationException("生产环境启用 OSS 存储时必须配置完整的 Endpoint/AccessKeyId/AccessKeySecret/BucketName。");
        }
    }
}

if (string.Equals(storageProvider, FileStorageOptions.ProviderLocal, StringComparison.OrdinalIgnoreCase))
{
    var localBasePath = string.IsNullOrWhiteSpace(fileStorage.BasePath) ? "uploads" : fileStorage.BasePath.Trim();
    var resolvedStoragePath = Path.IsPathRooted(localBasePath)
        ? localBasePath
        : Path.Combine(builder.Environment.ContentRootPath, localBasePath);
    Directory.CreateDirectory(resolvedStoragePath);
}

// ─── Antiforgery ───
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
builder.Services.AddSingleton<Atlas.Presentation.Shared.Services.MigrationGovernanceMetricsStore>();

// ─── Authentication / Authorization ───
builder.Services.AddAuthentication()
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
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(
                    string.Equals(jwt.SigningKey, "PLACEHOLDER__SET_VIA_ENV_JWT_SIGNING_KEY", StringComparison.Ordinal)
                        ? "temp-dev-signing-key-please-replace-at-runtime-in-production"
                        : jwt.SigningKey)),
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
            },
            OnAuthenticationFailed = context =>
            {
                if (context.Exception is SecurityTokenExpiredException)
                {
                    context.HttpContext.Items[AuthorizationContextKeys.AuthErrorCodeItemKey] = ErrorCodes.TokenExpired;
                }
                return Task.CompletedTask;
            },
            OnTokenValidated = async context =>
            {
                var principal = context.Principal;
                if (principal is null)
                {
                    var loc = context.HttpContext.RequestServices.GetService<IStringLocalizer<Messages>>();
                    context.Fail(loc?["InvalidToken"].Value ?? "Invalid token.");
                    return;
                }

                var tenantIdRaw = principal.FindFirstValue("tenant_id");
                if (!Guid.TryParse(tenantIdRaw, out var tenantGuid))
                {
                    var loc = context.HttpContext.RequestServices.GetService<IStringLocalizer<Messages>>();
                    context.Fail(loc?["TokenMissingTenant"].Value ?? "Token is missing a valid tenant claim.");
                    return;
                }

                var tenancyOptions = context.HttpContext.RequestServices
                    .GetRequiredService<Microsoft.Extensions.Options.IOptions<TenancyOptions>>()
                    .Value;
                if (context.HttpContext.Request.Headers.TryGetValue(tenancyOptions.HeaderName, out var tenantHeaderRaw)
                    && Guid.TryParse(tenantHeaderRaw.ToString(), out var headerTenantGuid)
                    && headerTenantGuid != tenantGuid)
                {
                    context.HttpContext.Items[AuthorizationContextKeys.AuthErrorCodeItemKey] = ErrorCodes.CrossTenantForbidden;
                    context.Fail("租户标识不一致");
                    return;
                }

                var userIdRaw = principal.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!long.TryParse(userIdRaw, out var userId))
                {
                    var loc = context.HttpContext.RequestServices.GetService<IStringLocalizer<Messages>>();
                    context.Fail(loc?["TokenMissingUser"].Value ?? "Token is missing a valid user identifier.");
                    return;
                }

                var sessionIdRaw = principal.FindFirstValue("sid");
                if (!long.TryParse(sessionIdRaw, out var sessionId))
                {
                    var loc = context.HttpContext.RequestServices.GetService<IStringLocalizer<Messages>>();
                    context.Fail(loc?["TokenMissingSession"].Value ?? "Token is missing a valid session identifier.");
                    return;
                }

                var tenantId = new TenantId(tenantGuid);

                var authCache = context.HttpContext.RequestServices
                    .GetRequiredService<Atlas.Application.Identity.Abstractions.IAuthCacheService>();
                var cached = await authCache.GetAsync(tenantId, userId, sessionId);

                if (cached is not null)
                {
                    if (!cached.IsUserActive)
                    {
                        var loc = context.HttpContext.RequestServices.GetService<IStringLocalizer<Messages>>();
                        context.Fail(loc?["UserDisabledOrNotExist"].Value ?? "User is disabled or does not exist.");
                        return;
                    }

                    if (cached.IsSessionRevoked || cached.SessionExpiresAt <= DateTimeOffset.UtcNow)
                    {
                        var loc = context.HttpContext.RequestServices.GetService<IStringLocalizer<Messages>>();
                        context.Fail(loc?["SessionRevoked"].Value ?? "Session has expired or been revoked.");
                        return;
                    }
                    return;
                }

                var userRepository = context.HttpContext.RequestServices.GetRequiredService<IUserAccountRepository>();
                var account = await userRepository.FindByIdAsync(tenantId, userId, context.HttpContext.RequestAborted);
                if (account is null || !account.IsActive)
                {
                    var loc = context.HttpContext.RequestServices.GetService<IStringLocalizer<Messages>>();
                    context.Fail(loc?["UserDisabledOrNotExist"].Value ?? "User is disabled or does not exist.");
                    return;
                }

                var sessionRepository = context.HttpContext.RequestServices.GetRequiredService<IAuthSessionRepository>();
                var session = await sessionRepository.FindByIdAsync(tenantId, sessionId, context.HttpContext.RequestAborted);
                if (session is null || session.UserId != userId || session.RevokedAt.HasValue || session.ExpiresAt <= DateTimeOffset.UtcNow)
                {
                    var loc = context.HttpContext.RequestServices.GetService<IStringLocalizer<Messages>>();
                    context.Fail(loc?["SessionRevoked"].Value ?? "Session has expired or been revoked.");
                    return;
                }

                await authCache.SetAsync(tenantId, userId, sessionId, new Atlas.Application.Identity.Abstractions.AuthValidationCacheEntry(
                    IsUserActive: account.IsActive,
                    UserId: userId,
                    SessionId: sessionId,
                    SessionExpiresAt: session.ExpiresAt,
                    IsSessionRevoked: session.RevokedAt.HasValue));
            }
        };
    })
    .AddCertificate(options =>
    {
        options.AllowedCertificateTypes = CertificateTypes.All;
    })
    .AddScheme<AuthenticationSchemeOptions, PatAuthenticationHandler>(PatAuthenticationHandler.SchemeName, _ => { })
    .AddScheme<AuthenticationSchemeOptions, OpenProjectAuthenticationHandler>(OpenProjectAuthenticationHandler.SchemeName, _ => { });

builder.Services.AddAuthorization(options =>
{
    var bearerPolicy = new AuthorizationPolicyBuilder(
            JwtBearerDefaults.AuthenticationScheme,
            CertificateAuthenticationDefaults.AuthenticationScheme,
            PatAuthenticationHandler.SchemeName)
        .RequireAuthenticatedUser()
        .Build();
    options.DefaultPolicy = bearerPolicy;
});
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, ApiAuthorizationMiddlewareResultHandler>();

// ─── Rate Limiter ───
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        var localizer = context.HttpContext.RequestServices.GetService<IStringLocalizer<Messages>>();
        var localized = localizer?["RateLimited"];
        var message = localized is null || localized.ResourceNotFound
            ? "Too many requests. Please try again later."
            : localized.Value;
        var payload = ApiResponse<object>.Fail("RATE_LIMITED", message, context.HttpContext.TraceIdentifier);
        await context.HttpContext.Response.WriteAsJsonAsync(payload, ct);
    };
    options.AddPolicy("auth", httpContext =>
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    });
});

// ─── Response Compression ───
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes
        .Where(mimeType => !string.Equals(mimeType, "text/event-stream", StringComparison.OrdinalIgnoreCase))
        .Concat(["application/json"]);
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});
builder.Services.Configure<BrotliCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);

// ─── MemoryCache + DatabaseConfigurationProvider ───
builder.Services.AddMemoryCache();
if (databaseConfigurationSource is not null)
{
    builder.Services.AddSingleton<DatabaseConfigurationProvider>(sp =>
    {
        var configurationRoot = sp.GetRequiredService<IConfiguration>() as IConfigurationRoot;
        var provider = configurationRoot?.Providers.OfType<DatabaseConfigurationProvider>().FirstOrDefault();
        return provider ?? throw new InvalidOperationException("DatabaseConfigurationProvider not found in configuration pipeline.");
    });
}

// ─── Application + Infrastructure ───
var appServices = builder.Services.AddAtlasApplicationShared(
    typeof(AlertMappingProfile).Assembly,
    typeof(ApprovalMappingProfile).Assembly,
    typeof(AssetsMappingProfile).Assembly,
    typeof(Atlas.Application.LogicFlow.Flows.Mappings.LogicFlowMappingProfile).Assembly,
    typeof(Atlas.Application.BatchProcess.Mappings.BatchProcessMappingProfile).Assembly,
    typeof(WebApiMappingProfile).Assembly)
    .AddAtlasApplicationPlatform();
appServices.AddAtlasApplicationRuntime();
builder.Services.AddAtlasInfrastructureShared(builder.Configuration)
    .AddAtlasInfrastructurePlatform(builder.Configuration)
    .AddAtlasInfrastructureAppRuntime(builder.Configuration);

// ─── i18n ───
builder.Services.AddScoped<Atlas.Application.System.Abstractions.ITenantService, Atlas.Infrastructure.Services.TenantService>();
builder.Services.AddLocalization(opts => opts.ResourcesPath = "");
builder.Services.Configure<Microsoft.AspNetCore.Builder.RequestLocalizationOptions>(opts =>
{
    var supportedCultures = new[] { "zh-CN", "en-US" };
    opts.SetDefaultCulture("zh-CN")
        .AddSupportedCultures(supportedCultures)
        .AddSupportedUICultures(supportedCultures);
    opts.ApplyCurrentCultureToResponseHeaders = true;
});

// ─── Hangfire (only register when setup is completed to avoid premature DB creation) ───
builder.Services.AddHangfire(config =>
    config.UseSQLiteStorage("hangfire-platformhost.db", new SQLiteStorageOptions
    {
        JournalMode = SQLiteStorageOptions.JournalModes.WAL,
        QueuePollInterval = TimeSpan.FromSeconds(hangfireQueuePollIntervalSeconds),
        JobExpirationCheckInterval = TimeSpan.FromMinutes(hangfireJobExpirationCheckIntervalMinutes)
    }));
if (runHangfireServer)
{
    builder.Services.AddHangfireServer(options =>
    {
        options.WorkerCount = hangfireWorkerCount;
        options.SchedulePollingInterval = TimeSpan.FromSeconds(hangfireSchedulePollingIntervalSeconds);
    });
}

// ─── WorkflowCore ───
builder.Services.AddWorkflowCore();
builder.Services.AddWorkflowCoreDsl(options =>
{
    options.AddNamespace("Atlas.WorkflowCore.Primitives");
});
builder.Services.AddHostedService<Atlas.Infrastructure.Services.WorkflowHostedService>();

// ─── YARP Reverse Proxy ───
builder.Services.AddSingleton<AppHostProxyConfigProvider>();
builder.Services.AddSingleton<IProxyConfigProvider>(sp => sp.GetRequiredService<AppHostProxyConfigProvider>());
builder.Services.AddReverseProxy();

// ─── Health Checks ───
builder.Services.AddHealthChecks();

var app = builder.Build();
var setupStateProvider = app.Services.GetRequiredService<ISetupStateProvider>();

// ─── AutoMapper validation (Dev only) ───
if (app.Environment.IsDevelopment() && validateAutoMapperOnStartup)
{
    using var scope = app.Services.CreateScope();
    var mapper = scope.ServiceProvider.GetRequiredService<IMapper>();
    try
    {
        mapper.ConfigurationProvider.AssertConfigurationIsValid();
    }
    catch (AutoMapper.AutoMapperConfigurationException ex)
    {
        Console.WriteLine("=== AutoMapper 配置错误详情 ===");
        Console.WriteLine(ex.Message);
        if (ex.InnerException != null)
        {
            Console.WriteLine("内部异常: " + ex.InnerException.Message);
        }
        throw;
    }
}

// ─── Middleware pipeline (same order as WebApi) ───
if (securityOptions.EnforceHttps)
{
    if (!app.Environment.IsDevelopment())
    {
        app.UseHsts();
    }
    app.UseHttpsRedirection();
}

app.UseSecurityHeaders();
app.UseHttpLogging();
app.UseRequestLocalization();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<SetupModeMiddleware>();
app.UseMiddleware<XssProtectionMiddleware>();
app.UseRateLimiter();
app.UseMiddleware<ApiVersionRewriteMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseOpenApi();
    app.UseSwaggerUi();
}

app.UseCors("PlatformHostCors");
app.UseResponseCompression();
app.UseMiddleware<ClientContextMiddleware>();
app.UseRouting();
app.UseWhen(
    context =>
    {
        if (!setupStateProvider.IsReady)
        {
            return false;
        }

        var path = context.Request.Path.Value ?? string.Empty;
        return !path.StartsWith("/api/v1/setup", StringComparison.OrdinalIgnoreCase)
            && !path.StartsWith("/health", StringComparison.OrdinalIgnoreCase)
            && !path.StartsWith("/internal/health", StringComparison.OrdinalIgnoreCase);
    },
    runtime =>
    {
        runtime.UseAuthentication();
        runtime.UseMiddleware<TenantContextMiddleware>();
        runtime.UseAuthorization();
        runtime.UseMiddleware<AppContextMiddleware>();
        runtime.UseMiddleware<AntiforgeryValidationMiddleware>();
        runtime.UseMiddleware<AppMembershipMiddleware>();
        runtime.UseMiddleware<ProjectContextMiddleware>();
        runtime.UseMiddleware<LicenseEnforcementMiddleware>();
        runtime.UseMiddleware<OpenApiGovernanceMiddleware>();
    });

app.MapHealthChecks("/internal/health/live");
app.MapHealthChecks("/internal/health/ready");
app.MapControllers();

if (runHangfireServer)
{
    var recurringJobs = app.Services.GetRequiredService<IRecurringJobManager>();
    recurringJobs.AddOrUpdate<Atlas.Infrastructure.Services.AiPlatform.WorkflowExecutionCleanupJob>(
        "workflow-execution-cleanup",
        job => job.ExecuteAsync(Atlas.Infrastructure.Services.AiPlatform.WorkflowExecutionCleanupJob.DefaultRetentionDays),
        Cron.Daily(3, 0));
}
app.MapHub<Atlas.Presentation.Shared.Hubs.NotificationHub>("/hubs/notification");
app.MapReverseProxy();
app.MapGet("/", () => Results.Ok(new
{
    host = "PlatformHost",
    status = "running",
    environment = app.Environment.EnvironmentName
}));

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
    Console.WriteLine("Atlas PlatformHost 启动成功");
    Console.WriteLine($"环境: {app.Environment.EnvironmentName}");

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
