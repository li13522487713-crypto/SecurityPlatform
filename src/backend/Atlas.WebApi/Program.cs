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
using Atlas.WebApi.Middlewares;
using Hangfire;
using Hangfire.Storage.SQLite;
using Atlas.WebApi.Tenancy;
using Atlas.WorkflowCore;
using Atlas.WorkflowCore.DSL;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Certificate;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using NLog.Web;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Atlas.WebApi.Authorization;
using Atlas.WebApi.Filters;
using Atlas.WebApi.Mappings;
using Atlas.WebApi.Security;
using Atlas.Application.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Infrastructure.Configuration;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.ResponseCompression;
using Atlas.Core.Models;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Microsoft.Extensions.Localization;
using System.IO.Compression;

var builder = WebApplication.CreateBuilder(args);

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
        encryptionKey);
    builder.Configuration.Sources.Add(databaseConfigurationSource);
}

var validateAutoMapperOnStartup = builder.Configuration.GetValue("AutoMapper:ValidateOnStartup", false);
var runHangfireServer = builder.Configuration.GetValue("Hangfire:RunServer", !builder.Environment.IsDevelopment());
var hangfireWorkerCount = builder.Configuration.GetValue("Hangfire:WorkerCount", builder.Environment.IsDevelopment() ? 1 : 3);
var hangfireQueuePollIntervalSeconds = builder.Configuration.GetValue("Hangfire:QueuePollIntervalSeconds", builder.Environment.IsDevelopment() ? 30 : 15);
var hangfireSchedulePollingIntervalSeconds = builder.Configuration.GetValue("Hangfire:SchedulePollingIntervalSeconds", builder.Environment.IsDevelopment() ? 60 : 15);
var hangfireJobExpirationCheckIntervalMinutes = builder.Configuration.GetValue("Hangfire:JobExpirationCheckIntervalMinutes", builder.Environment.IsDevelopment() ? 360 : 60);

if (builder.Environment.IsDevelopment())
{
    builder.WebHost.UseUrls("http://localhost:5000");
}

builder.Logging.ClearProviders();
builder.Host.UseNLog();

builder.Services.AddControllers(options =>
{
    options.Filters.Add<IdempotencyFilter>();
})
.AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new Atlas.WebApi.Json.FlexibleLongJsonConverter());
    options.JsonSerializerOptions.Converters.Add(new Atlas.WebApi.Json.FlexibleNullableLongJsonConverter());
    options.JsonSerializerOptions.Converters.Add(new Atlas.WebApi.Json.SensitiveObjectConverterFactory());
});

// 配置 NSwag OpenAPI 文档生成
builder.Services.AddOpenApiDocument(config =>
{
    config.Title = "Atlas Security Platform API";
    config.Version = "v1";
    config.Description = "Atlas 安全平台 API 文档（符合等保2.0标准）";
    config.UseControllerSummaryAsTagDescription = true;
    config.PostProcess = document =>
    {
        document.Info.Contact = new NSwag.OpenApiContact
        {
            Name = "Atlas Security Team",
            Email = "security@atlas.com"
        };
        document.Info.License = new NSwag.OpenApiLicense
        {
            Name = "Proprietary"
        };
    };
});

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
builder.Services.Configure<Atlas.WebApi.Identity.AppOptions>(builder.Configuration.GetSection("App"));
builder.Services.Configure<Atlas.Application.Options.DatabaseInitializerOptions>(builder.Configuration.GetSection("DatabaseInitializer"));

// OIDC/SSO 支持（可选，通过 Oidc:Enabled 控制；支持多 IdP）
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
            // 登录成功后由 OidcAccountMapper 完成账号映射，并颁发 Atlas JWT
            options.Events.OnTokenValidated = async ctx =>
            {
                if (ctx.Principal is null) return;
                var mapper = ctx.HttpContext.RequestServices.GetRequiredService<Atlas.Infrastructure.Security.OidcAccountMapper>();
                var tenantProvider = ctx.HttpContext.RequestServices.GetRequiredService<Atlas.Core.Tenancy.ITenantProvider>();
                var tenantId = tenantProvider.GetTenantId();
                await mapper.MapOrCreateAsync(ctx.Principal, tenantId, provider.ProviderId, ctx.HttpContext.RequestAborted);
            };
        });
    }
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("WebAppCors", policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? Array.Empty<string>();
        policy.WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials(); // 允许携带凭证（cookies）
    });
});
builder.Services.AddHttpClient("app-runtime-proxy", client =>
{
    var baseUrl = builder.Configuration["Atlas:Runtime:AppHostBaseUrl"] ?? "http://localhost:5002";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpLogging(options =>
{
    options.LoggingFields = HttpLoggingFields.RequestPath
        | HttpLoggingFields.RequestMethod
        | HttpLoggingFields.RequestQuery
        | HttpLoggingFields.ResponseStatusCode
        | HttpLoggingFields.Duration;
});

var serviceName = "Atlas.WebApi";
var serviceVersion = typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0";
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
            tracing.AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri(otlpEndpoint);
            });
        }
        else if (builder.Environment.IsDevelopment()
            && builder.Configuration.GetValue<bool>("OpenTelemetry:EnableConsoleExporter"))
        {
            // 仅在显式开启时才输出到控制台（默认关闭，避免大量 I/O 拖慢响应）
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
            metrics.AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri(otlpEndpoint);
            });
        }
        else if (builder.Environment.IsDevelopment()
            && builder.Configuration.GetValue<bool>("OpenTelemetry:EnableConsoleExporter"))
        {
            // 仅在显式开启时才输出到控制台（默认关闭，避免大量 I/O 拖慢响应）
            metrics.AddConsoleExporter();
        }
    });

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<Atlas.Core.Tenancy.ITenantProvider, HttpContextTenantProvider>();
builder.Services.AddScoped<Atlas.Core.Identity.ICurrentUserAccessor, Atlas.WebApi.Identity.HttpContextCurrentUserAccessor>();
builder.Services.AddScoped<Atlas.Core.Identity.IClientContextAccessor, Atlas.WebApi.Identity.HttpContextClientContextAccessor>();
builder.Services.AddScoped<Atlas.Core.Identity.IAppContextAccessor, Atlas.WebApi.Identity.HttpContextAppContextAccessor>();
builder.Services.AddScoped<Atlas.Core.Identity.IProjectContextAccessor, Atlas.WebApi.Identity.HttpContextProjectContextAccessor>();
builder.Services.AddScoped<IdempotencyFilter>();

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblies([
    typeof(Atlas.Application.Validators.AuthTokenRequestValidator).Assembly,         // Atlas.Application
    typeof(Atlas.Application.Approval.Validators.ApprovalFlowDefinitionCreateRequestValidator).Assembly, // Atlas.Application.Approval
    typeof(Atlas.Application.Assets.Validators.AssetValidator).Assembly,             // Atlas.Application.Assets
    typeof(Atlas.Application.Alert.Validators.AlertRecordValidator).Assembly,        // Atlas.Application.Alert
    typeof(Atlas.Application.Audit.Validators.AuditRecordValidator).Assembly,        // Atlas.Application.Audit
    typeof(Atlas.Application.AgentTeam.Validators.AgentTeamCreateRequestValidator).Assembly, // Atlas.Application.AgentTeam
    typeof(Atlas.Application.Workflow.Validators.PublishEventRequestValidator).Assembly, // Atlas.Application.Workflow
    typeof(Atlas.WebApi.Validators.ChangePasswordViewModelValidator).Assembly,       // Atlas.WebApi
]);

var securityOptions = builder.Configuration.GetSection("Security").Get<SecurityOptions>() ?? new SecurityOptions();
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
var fileStorage = builder.Configuration.GetSection("FileStorage").Get<FileStorageOptions>() ?? new FileStorageOptions();
var storageProvider = fileStorage.Provider?.Trim().ToLowerInvariant() ?? FileStorageOptions.ProviderLocal;
if (!builder.Environment.IsDevelopment())
{
    if (string.IsNullOrWhiteSpace(jwt.SigningKey)
        || jwt.SigningKey.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase)
        || jwt.SigningKey.Length < 32)
    {
        throw new InvalidOperationException("生产环境必须配置长度不少于32位的JWT SigningKey。");
    }

    if (string.IsNullOrWhiteSpace(fileStorage.SignedUrlSecret)
        || string.Equals(fileStorage.SignedUrlSecret, FileStorageOptions.UnsafeDefaultSignedUrlSecret, StringComparison.Ordinal)
        || fileStorage.SignedUrlSecret.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase)
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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            NameClaimType = JwtRegisteredClaimNames.Sub,
            RoleClaimType = ClaimTypes.Role,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                // 优先从httpOnly cookie读取token（安全加固）
                var accessToken = context.Request.Cookies["access_token"];
                if (!string.IsNullOrEmpty(accessToken))
                {
                    context.Token = accessToken;
                }
                // 向后兼容：如果cookie中没有token，则从Authorization header读取
                // JwtBearer中间件会自动从header读取

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

                // 优先读缓存，减少 DB 查询（TTL 60 秒）
                var authCache = context.HttpContext.RequestServices
                    .GetRequiredService<Atlas.Application.Identity.Abstractions.IAuthCacheService>();
                var cached = authCache.Get(tenantId, userId, sessionId);

                if (cached is not null)
                {
                    // 缓存命中：直接使用缓存结果验证
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

                    return; // 缓存验证通过
                }

                // 缓存未命中：查数据库
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

                // 写入缓存，下次请求直接命中
                authCache.Set(tenantId, userId, sessionId, new Atlas.Application.Identity.Abstractions.AuthValidationCacheEntry(
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
    .AddScheme<AuthenticationSchemeOptions, PatAuthenticationHandler>(PatAuthenticationHandler.SchemeName, options =>
    {
    })
    .AddScheme<AuthenticationSchemeOptions, OpenProjectAuthenticationHandler>(OpenProjectAuthenticationHandler.SchemeName, options =>
    {
    });

builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new AuthorizationPolicyBuilder(
            JwtBearerDefaults.AuthenticationScheme,
            CertificateAuthenticationDefaults.AuthenticationScheme,
            PatAuthenticationHandler.SchemeName)
        .RequireAuthenticatedUser()
        .Build();
});
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, ApiAuthorizationMiddlewareResultHandler>();
builder.Services.AddSingleton<Atlas.WebApi.Services.MigrationGovernanceMetricsStore>();

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

    // Auth endpoints: 10 requests per minute per IP
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

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(["application/json"]);
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});
builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest;
});
builder.Services.Configure<GzipCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest;
});

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
builder.Services.AddAtlasApplicationShared(
    typeof(AlertMappingProfile).Assembly,
    typeof(ApprovalMappingProfile).Assembly,
    typeof(AssetsMappingProfile).Assembly,
    typeof(Atlas.Application.LogicFlow.Flows.Mappings.LogicFlowMappingProfile).Assembly,
    typeof(Atlas.Application.BatchProcess.Mappings.BatchProcessMappingProfile).Assembly,
    typeof(WebApiMappingProfile).Assembly)
    .AddAtlasApplicationPlatform()
    .AddAtlasApplicationRuntime();
builder.Services.AddAtlasInfrastructureShared(builder.Configuration)
    .AddAtlasInfrastructurePlatform(builder.Configuration)
    .AddAtlasInfrastructureAppRuntime(builder.Configuration);

// 国际化（i18n）：支持中文和英文
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

// Hangfire 定时任务（使用 SQLite 存储）
// SQLite 同一时刻只允许一个写操作，过多 Worker 只会互相争锁；
// WAL 模式允许读写并发，减少锁等待；Worker 数设为 3 匹配实际并发能力。
builder.Services.AddHangfire(config =>
    config.UseSQLiteStorage("hangfire.db", new Hangfire.Storage.SQLite.SQLiteStorageOptions
    {
        // WAL 模式：读写可并发，减少锁等待
        JournalMode = Hangfire.Storage.SQLite.SQLiteStorageOptions.JournalModes.WAL,
        QueuePollInterval = TimeSpan.FromSeconds(hangfireQueuePollIntervalSeconds),
        JobExpirationCheckInterval = TimeSpan.FromMinutes(hangfireJobExpirationCheckIntervalMinutes)
    }));
if (runHangfireServer)
{
    builder.Services.AddHangfireServer(options =>
    {
        // SQLite 单写限制：3 个 Worker 是比较合理的上限，过多反而因争锁而变慢
        options.WorkerCount = hangfireWorkerCount;
        options.SchedulePollingInterval = TimeSpan.FromSeconds(hangfireSchedulePollingIntervalSeconds);
    });
}



// 添加 WorkflowCore 工作流引擎
builder.Services.AddWorkflowCore();
builder.Services.AddWorkflowCoreDsl(options =>
{
    options.AddNamespace("Atlas.WorkflowCore.Primitives");
});
builder.Services.AddHostedService<Atlas.Infrastructure.Services.WorkflowHostedService>();

var app = builder.Build();

if (app.Environment.IsDevelopment() && validateAutoMapperOnStartup)
{
    // AutoMapper 配置验证仅在开发环境执行，生产环境依赖 CI 管线保证
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

if (securityOptions.EnforceHttps)
{
    if (!app.Environment.IsDevelopment())
    {
        app.UseHsts();
    }

    app.UseHttpsRedirection();
}

// 添加安全HTTP响应头（防御XSS、Clickjacking等攻击）
app.UseSecurityHeaders();

app.UseHttpLogging();
app.UseRequestLocalization();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<XssProtectionMiddleware>();
app.UseRateLimiter();
app.UseMiddleware<ApiVersionRewriteMiddleware>();

if (app.Environment.IsDevelopment())
{
    // NSwag 中间件：生成 OpenAPI 规范和 Swagger UI
    app.UseOpenApi();       // 提供 /swagger/v1/swagger.json
    app.UseSwaggerUi();     // 提供 /swagger 交互式文档
}

app.UseCors("WebAppCors");
app.UseResponseCompression();
app.UseMiddleware<ClientContextMiddleware>();
app.UseRouting();
app.UseAuthentication();
app.UseMiddleware<AppContextMiddleware>();
app.UseMiddleware<AntiforgeryValidationMiddleware>();
app.UseMiddleware<TenantContextMiddleware>();
app.UseMiddleware<AppMembershipMiddleware>();
app.UseMiddleware<ProjectContextMiddleware>();
app.UseMiddleware<LicenseEnforcementMiddleware>();
app.UseAuthorization();
app.UseMiddleware<OpenApiGovernanceMiddleware>();

app.MapControllers();

app.Lifetime.ApplicationStarted.Register(() =>
{
    var addresses = app.Urls
        .OrderBy(static address => address, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    var logo = string.Join(Environment.NewLine, [
        "  ___   _   _               _",
        " / _ | / | / /___ ______ __(_)",
        "/ __ |/  |/ / -_) __/ // / /",
        "/_/ |_/_/|_/\\__/_/  \\_,_/_/"
    ]);

    Console.WriteLine();
    Console.WriteLine(logo);
    Console.WriteLine("Atlas Security Platform 启动成功");
    Console.WriteLine($"环境: {app.Environment.EnvironmentName}");

    if (addresses.Length == 0)
    {
        Console.WriteLine("启动地址: 未获取到监听地址");
    }
    else
    {
        foreach (var address in addresses)
        {
            Console.WriteLine($"启动地址: {address}");
        }

        if (app.Environment.IsDevelopment())
        {
            foreach (var address in addresses)
            {
                Console.WriteLine($"Swagger: {address.TrimEnd('/')}/swagger");
            }
        }
    }

    Console.WriteLine();
});

app.Run();
