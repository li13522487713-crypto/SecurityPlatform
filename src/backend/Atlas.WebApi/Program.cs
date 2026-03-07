using AutoMapper;
using FluentValidation;
using FluentValidation.AspNetCore;
using Atlas.Application;
using Atlas.Application.Options;
using Atlas.Infrastructure;
using Atlas.WebApi.Middlewares;
using Hangfire;
using Hangfire.Storage.SQLite;
using Atlas.WebApi.Tenancy;
using Atlas.WorkflowCore;
using Atlas.WorkflowCore.DSL;
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
using Atlas.Application.Abstractions;
using Atlas.Core.Tenancy;
using Microsoft.AspNetCore.Authorization.Policy;
using Atlas.Core.Models;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

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

// OIDC 支持（可选，通过 Oidc:Enabled 控制）
builder.Services.Configure<Atlas.Infrastructure.Security.OidcOptions>(builder.Configuration.GetSection("Oidc"));
builder.Services.AddScoped<Atlas.Infrastructure.Security.OidcAccountMapper>();
var oidcEnabled = builder.Configuration.GetValue<bool>("Oidc:Enabled");
if (oidcEnabled)
{
    var oidcAuthority = builder.Configuration["Oidc:Authority"] ?? string.Empty;
    var oidcClientId = builder.Configuration["Oidc:ClientId"] ?? string.Empty;

    builder.Services.AddAuthentication()
        .AddOpenIdConnect("oidc", options =>
        {
            options.Authority = oidcAuthority;
            options.ClientId = oidcClientId;
            options.ClientSecret = builder.Configuration["Oidc:ClientSecret"];
            options.CallbackPath = builder.Configuration["Oidc:CallbackPath"] ?? "/auth/oidc/callback";
            options.ResponseType = "code";
            options.SaveTokens = false;
            var scopes = builder.Configuration.GetSection("Oidc:Scopes").Get<string[]>() ?? ["openid", "profile", "email"];
            foreach (var scope in scopes)
            {
                options.Scope.Add(scope);
            }
        });
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
    typeof(Atlas.Application.Workflow.Validators.PublishEventRequestValidator).Assembly, // Atlas.Application.Workflow
    typeof(Atlas.WebApi.Validators.ChangePasswordViewModelValidator).Assembly,       // Atlas.WebApi
]);

var securityOptions = builder.Configuration.GetSection("Security").Get<SecurityOptions>() ?? new SecurityOptions();
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
if (!builder.Environment.IsDevelopment())
{
    if (string.IsNullOrWhiteSpace(jwt.SigningKey)
        || jwt.SigningKey.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase)
        || jwt.SigningKey.Length < 32)
    {
        throw new InvalidOperationException("生产环境必须配置长度不少于32位的JWT SigningKey。");
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
                    context.Fail("无效令牌。");
                    return;
                }

                var tenantIdRaw = principal.FindFirstValue("tenant_id");
                if (!Guid.TryParse(tenantIdRaw, out var tenantGuid))
                {
                    context.Fail("令牌缺少有效租户信息。");
                    return;
                }

                var userIdRaw = principal.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!long.TryParse(userIdRaw, out var userId))
                {
                    context.Fail("令牌缺少有效用户标识。");
                    return;
                }

                var tenantId = new TenantId(tenantGuid);
                var userRepository = context.HttpContext.RequestServices.GetRequiredService<IUserAccountRepository>();
                var account = await userRepository.FindByIdAsync(tenantId, userId, context.HttpContext.RequestAborted);
                if (account is null || !account.IsActive)
                {
                    context.Fail("用户已禁用或不存在。");
                    return;
                }

                var sessionIdRaw = principal.FindFirstValue("sid");
                if (!long.TryParse(sessionIdRaw, out var sessionId))
                {
                    context.Fail("令牌缺少有效会话标识。");
                    return;
                }

                var sessionRepository = context.HttpContext.RequestServices.GetRequiredService<IAuthSessionRepository>();
                var session = await sessionRepository.FindByIdAsync(tenantId, sessionId, context.HttpContext.RequestAborted);
                if (session is null || session.UserId != userId || session.RevokedAt.HasValue || session.ExpiresAt <= DateTimeOffset.UtcNow)
                {
                    context.Fail("会话已失效。");
                }
            }
        };
    })
    .AddCertificate(options =>
    {
        options.AllowedCertificateTypes = CertificateTypes.All;
    });

builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new AuthorizationPolicyBuilder(
            JwtBearerDefaults.AuthenticationScheme,
            CertificateAuthenticationDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser()
        .Build();
});
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, ApiAuthorizationMiddlewareResultHandler>();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        var payload = ApiResponse<object>.Fail("RATE_LIMITED", "请求过于频繁，请稍后再试", context.HttpContext.TraceIdentifier);
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

builder.Services.AddMemoryCache();
builder.Services.AddAtlasApplication();
builder.Services.AddAtlasInfrastructure(builder.Configuration);

// 国际化（i18n）：支持中文和英文
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
builder.Services.AddHangfire(config =>
    config.UseSQLiteStorage("hangfire.db"));
builder.Services.AddHangfireServer();

// 添加 WorkflowCore 工作流引擎
builder.Services.AddWorkflowCore();
builder.Services.AddWorkflowCoreDsl(options =>
{
    options.AddNamespace("Atlas.WorkflowCore.Primitives");
});
builder.Services.AddHostedService<Atlas.Infrastructure.Services.WorkflowHostedService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
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
app.UseMiddleware<AppContextMiddleware>();
app.UseMiddleware<ClientContextMiddleware>();
app.UseRouting();
app.UseAuthentication();
app.UseMiddleware<AntiforgeryValidationMiddleware>();
app.UseMiddleware<TenantContextMiddleware>();
app.UseMiddleware<ProjectContextMiddleware>();
app.UseMiddleware<LicenseEnforcementMiddleware>();
app.UseAuthorization();

app.MapControllers();

app.Run();
