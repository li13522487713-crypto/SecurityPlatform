using Atlas.Application.Identity.Abstractions;
using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.Platform.Repositories;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Atlas.Presentation.Shared.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Certificate;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Presentation.Shared.Middlewares;

/// <summary>
/// 应用成员隔离中间件：
/// 对要求 app:user / app:admin 权限的接口，在应用关闭共享用户时强制校验成员归属。
/// </summary>
public sealed class AppMembershipMiddleware
{
    private readonly RequestDelegate _next;

    public AppMembershipMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!RequiresAppMembership(context))
        {
            await _next(context);
            return;
        }

        await EnsureAuthenticatedPrincipalAsync(context);

        var tenantProvider = context.RequestServices.GetRequiredService<ITenantProvider>();
        var tenantId = tenantProvider.GetTenantId();
        if (tenantId.IsEmpty)
        {
            await WriteErrorAsync(
                context,
                StatusCodes.Status400BadRequest,
                ErrorCodes.ValidationError,
                "缺少租户标识。");
            return;
        }

        var appContextAccessor = context.RequestServices.GetRequiredService<IAppContextAccessor>();
        var appId = ResolveAppId(context, appContextAccessor);
        if (!appId.HasValue)
        {
            await WriteErrorAsync(
                context,
                StatusCodes.Status400BadRequest,
                ErrorCodes.AppContextRequired,
                "缺少应用上下文。");
            return;
        }

        var lowCodeAppRepository = context.RequestServices.GetRequiredService<ILowCodeAppRepository>();
        var app = await lowCodeAppRepository.GetByIdAsync(tenantId, appId.Value, context.RequestAborted);
        if (app is null)
        {
            await WriteErrorAsync(
                context,
                StatusCodes.Status404NotFound,
                ErrorCodes.NotFound,
                "应用实例不存在。");
            return;
        }

        var currentUserAccessor = context.RequestServices.GetRequiredService<ICurrentUserAccessor>();
        var currentUser = currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            await WriteErrorAsync(
                context,
                StatusCodes.Status401Unauthorized,
                ErrorCodes.Unauthorized,
                "缺少用户上下文。");
            return;
        }

        if (currentUser.IsPlatformAdmin)
        {
            await _next(context);
            return;
        }

        var permissionDecisionService = context.RequestServices.GetRequiredService<IPermissionDecisionService>();
        var isSystemAdmin = await permissionDecisionService.IsSystemAdminAsync(
            tenantId,
            currentUser.UserId,
            context.RequestAborted);
        if (isSystemAdmin)
        {
            await _next(context);
            return;
        }

        var appMemberRepository = context.RequestServices.GetRequiredService<IAppMemberRepository>();
        var hasMembership = await appMemberRepository.ExistsAsync(
            tenantId,
            appId.Value,
            currentUser.UserId,
            context.RequestAborted);
        if (hasMembership)
        {
            await _next(context);
            return;
        }

        // 兼容首次切换到“非共享用户”模式时尚未配置成员列表的场景：
        // 允许应用创建者进入并完成成员初始化，其余用户仍需明确入组。
        var hasAnyMember = await appMemberRepository.ExistsAnyAsync(
            tenantId,
            appId.Value,
            context.RequestAborted);
        if (!hasAnyMember && app.CreatedBy == currentUser.UserId)
        {
            await _next(context);
            return;
        }

        await WriteErrorAsync(
            context,
            StatusCodes.Status403Forbidden,
            ErrorCodes.Forbidden,
            "当前用户未分配应用成员权限。");
    }

    private static long? ResolveAppId(HttpContext context, IAppContextAccessor appContextAccessor)
    {
        var resolved = appContextAccessor.ResolveAppId();
        if (resolved.HasValue)
        {
            return resolved;
        }

        if (!context.Request.Headers.TryGetValue("X-App-Id", out var headerValue))
        {
            return null;
        }

        var appIdText = headerValue.ToString();
        return long.TryParse(appIdText, out var appId) && appId > 0
            ? appId
            : null;
    }

    private static async Task EnsureAuthenticatedPrincipalAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            return;
        }

        var schemes = new[]
        {
            JwtBearerDefaults.AuthenticationScheme,
            CertificateAuthenticationDefaults.AuthenticationScheme,
            PatAuthenticationHandler.SchemeName
        };

        foreach (var scheme in schemes)
        {
            var result = await context.AuthenticateAsync(scheme);
            if (result.Succeeded && result.Principal is not null)
            {
                context.User = result.Principal;
                return;
            }
        }
    }

    private static bool RequiresAppMembership(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        if (endpoint?.Metadata.GetMetadata<IAllowAnonymous>() is not null)
        {
            return false;
        }

        var authorizeData = endpoint?.Metadata.GetOrderedMetadata<IAuthorizeData>();
        if (authorizeData is null || authorizeData.Count == 0)
        {
            return false;
        }

        return authorizeData.Any(meta =>
            string.Equals(meta.Policy, PermissionPolicies.AppUser, StringComparison.Ordinal)
            || string.Equals(meta.Policy, PermissionPolicies.AppAdmin, StringComparison.Ordinal));
    }

    private static async Task WriteErrorAsync(HttpContext context, int statusCode, string code, string message)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        var payload = ApiResponse<object?>.Fail(code, message, context.TraceIdentifier);
        await context.Response.WriteAsJsonAsync(payload);
    }
}
