using AutoMapper;
using FluentValidation;
using FluentValidation.AspNetCore;
using Atlas.Application;
using Atlas.Application.Options;
using Atlas.Infrastructure;
using Atlas.WebApi.Middlewares;
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
using Microsoft.AspNetCore.Authorization.Policy;
using Atlas.Core.Models;

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
});
builder.Services.AddOpenApi();

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<SecurityOptions>(builder.Configuration.GetSection("Security"));
builder.Services.Configure<PasswordPolicyOptions>(builder.Configuration.GetSection("Security:PasswordPolicy"));
builder.Services.Configure<LockoutPolicyOptions>(builder.Configuration.GetSection("Security:LockoutPolicy"));
builder.Services.Configure<BootstrapAdminOptions>(builder.Configuration.GetSection("Security:BootstrapAdmin"));
builder.Services.Configure<ApprovalSeedDataOptions>(builder.Configuration.GetSection("Approval:SeedData"));
builder.Services.Configure<TenancyOptions>(builder.Configuration.GetSection("Tenancy"));
builder.Services.Configure<IdempotencyOptions>(builder.Configuration.GetSection("Idempotency"));
builder.Services.Configure<TableViewDefaultOptions>(builder.Configuration.GetSection("TableViewDefaults"));
builder.Services.Configure<Atlas.WebApi.Identity.AppOptions>(builder.Configuration.GetSection("App"));

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

builder.Services.AddAtlasApplication();
builder.Services.AddAtlasInfrastructure(builder.Configuration);

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
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseRateLimiter();
app.UseMiddleware<ApiVersionRewriteMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("WebAppCors");
app.UseMiddleware<AppContextMiddleware>();
app.UseMiddleware<ClientContextMiddleware>();
app.UseRouting();
app.UseAuthentication();
app.UseMiddleware<AntiforgeryValidationMiddleware>();
app.UseMiddleware<TenantContextMiddleware>();
app.UseMiddleware<ProjectContextMiddleware>();
app.UseAuthorization();

app.MapControllers();

app.Run();
