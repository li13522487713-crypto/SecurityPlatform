using Atlas.Application.Identity.Abstractions;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.WebApi.Identity;
using Atlas.WebApi.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.WebApi.Middlewares;

public sealed class ProjectContextMiddleware
{
    public const string ProjectHeaderName = "X-Project-Id";

    private readonly RequestDelegate _next;

    public ProjectContextMiddleware(
        RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var appContextAccessor = context.RequestServices.GetRequiredService<IAppContextAccessor>();
        var tenantProvider = context.RequestServices.GetRequiredService<ITenantProvider>();
        var appConfigQueryService = context.RequestServices.GetRequiredService<IAppConfigQueryService>();
        var currentUserAccessor = context.RequestServices.GetRequiredService<ICurrentUserAccessor>();
        var projectUserRepository = context.RequestServices.GetRequiredService<Atlas.Application.Identity.Repositories.IProjectUserRepository>();
        var permissionDecisionService = context.RequestServices.GetRequiredService<IPermissionDecisionService>();

        var endpoint = context.GetEndpoint();
        var allowAnonymous = endpoint?.Metadata.GetMetadata<IAllowAnonymous>() is not null;
        var path = context.Request.Path.Value ?? string.Empty;
        var skipProjectCheck = allowAnonymous
            || StartsWithAny(path, "/health", "/api/v1/health")
            || StartsWithAny(path, "/openapi")
            || StartsWithAny(path, "/api/auth", "/api/v1/auth")
            || StartsWithAny(path, "/api/apps", "/api/v1/apps")
            || StartsWithAny(path, "/api/projects", "/api/v1/projects")
            || StartsWithAny(path, "/api/personal-access-tokens", "/api/v1/personal-access-tokens")
            || StartsWithAny(path, "/api/notifications", "/api/v1/notifications");

        if (string.Equals(
                context.User?.Identity?.AuthenticationType,
                PatAuthenticationHandler.SchemeName,
                StringComparison.OrdinalIgnoreCase))
        {
            skipProjectCheck = true;
        }

        if (skipProjectCheck)
        {
            await _next(context);
            return;
        }

        var appContext = appContextAccessor.GetCurrent();
        var tenantId = tenantProvider.GetTenantId();
        if (tenantId.IsEmpty)
        {
            await WriteProjectErrorAsync(context, StatusCodes.Status400BadRequest, ErrorCodes.ValidationError, "缺少租户标识");
            return;
        }

        var appConfig = await appConfigQueryService.GetByAppIdAsync(appContext.AppId, tenantId, context.RequestAborted);
        if (appConfig is null || !appConfig.EnableProjectScope)
        {
            context.Items[HttpContextProjectContextAccessor.ProjectContextItemKey] = new ProjectContext(false, null);
            await _next(context);
            return;
        }

        if (!TryResolveProjectId(context, out var projectId))
        {
            await WriteProjectErrorAsync(context, StatusCodes.Status400BadRequest, ErrorCodes.ProjectRequired, "缺少或无效项目标识");
            return;
        }

        var currentUser = currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            await WriteProjectErrorAsync(context, StatusCodes.Status401Unauthorized, ErrorCodes.Unauthorized, "缺少用户上下文");
            return;
        }

        var isSystemRole = await permissionDecisionService.IsSystemAdminAsync(
            tenantId,
            currentUser.UserId,
            context.RequestAborted);
        if (!isSystemRole)
        {
            var hasMembership = await projectUserRepository.ExistsAsync(tenantId, projectId, currentUser.UserId, context.RequestAborted);
            if (!hasMembership)
            {
                await WriteProjectErrorAsync(context, StatusCodes.Status403Forbidden, ErrorCodes.ProjectForbidden, "当前用户未分配该项目");
                return;
            }
        }

        context.Items[HttpContextProjectContextAccessor.ProjectContextItemKey] = new ProjectContext(true, projectId);
        await _next(context);
    }

    private static bool TryResolveProjectId(HttpContext context, out long projectId)
    {
        projectId = 0;
        if (!context.Request.Headers.TryGetValue(ProjectHeaderName, out var headerValue))
        {
            return false;
        }

        return long.TryParse(headerValue.ToString(), out projectId);
    }

    private static async Task WriteProjectErrorAsync(HttpContext context, int statusCode, string code, string message)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var payload = ApiResponse<object?>.Fail(code, message, context.TraceIdentifier);
        await context.Response.WriteAsJsonAsync(payload);
    }

    private static bool StartsWithAny(string path, params string[] prefixes)
    {
        return prefixes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }
}
