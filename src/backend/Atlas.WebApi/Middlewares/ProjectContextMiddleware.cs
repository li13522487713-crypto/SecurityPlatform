using Atlas.Application.Identity.Abstractions;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.WebApi.Identity;
using Microsoft.AspNetCore.Authorization;

namespace Atlas.WebApi.Middlewares;

public sealed class ProjectContextMiddleware
{
    public const string ProjectHeaderName = "X-Project-Id";

    private readonly RequestDelegate _next;
    private readonly IAppContextAccessor _appContextAccessor;
    private readonly ITenantProvider _tenantProvider;
    private readonly IAppConfigQueryService _appConfigQueryService;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly Atlas.Application.Identity.Repositories.IProjectUserRepository _projectUserRepository;

    public ProjectContextMiddleware(
        RequestDelegate next,
        IAppContextAccessor appContextAccessor,
        ITenantProvider tenantProvider,
        IAppConfigQueryService appConfigQueryService,
        ICurrentUserAccessor currentUserAccessor,
        Atlas.Application.Identity.Repositories.IProjectUserRepository projectUserRepository)
    {
        _next = next;
        _appContextAccessor = appContextAccessor;
        _tenantProvider = tenantProvider;
        _appConfigQueryService = appConfigQueryService;
        _currentUserAccessor = currentUserAccessor;
        _projectUserRepository = projectUserRepository;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        var allowAnonymous = endpoint?.Metadata.GetMetadata<IAllowAnonymous>() is not null;
        var path = context.Request.Path.Value ?? string.Empty;
        var skipProjectCheck = allowAnonymous
            || path.StartsWith("/health", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/openapi", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/auth", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/apps", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/projects", StringComparison.OrdinalIgnoreCase);

        if (skipProjectCheck)
        {
            await _next(context);
            return;
        }

        var appContext = _appContextAccessor.GetCurrent();
        var tenantId = _tenantProvider.GetTenantId();
        if (tenantId.IsEmpty)
        {
            await WriteProjectErrorAsync(context, StatusCodes.Status400BadRequest, ErrorCodes.ValidationError, "缺少租户标识");
            return;
        }

        var appConfig = await _appConfigQueryService.GetByAppIdAsync(appContext.AppId, tenantId, context.RequestAborted);
        if (appConfig is null || !appConfig.EnableProjectScope)
        {
            context.Items[HttpContextProjectContextAccessor.ProjectContextItemKey] = new ProjectContext(false, null);
            await _next(context);
            return;
        }

        if (!TryResolveProjectId(context, out var projectId))
        {
            await WriteProjectErrorAsync(context, StatusCodes.Status400BadRequest, ErrorCodes.ValidationError, "缺少或无效项目标识");
            return;
        }

        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            await WriteProjectErrorAsync(context, StatusCodes.Status401Unauthorized, ErrorCodes.Unauthorized, "缺少用户上下文");
            return;
        }

        if (!IsSystemRole(currentUser.Roles))
        {
            var hasMembership = await _projectUserRepository.ExistsAsync(tenantId, projectId, currentUser.UserId, context.RequestAborted);
            if (!hasMembership)
            {
                await WriteProjectErrorAsync(context, StatusCodes.Status403Forbidden, ErrorCodes.Forbidden, "当前用户未分配该项目");
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

    private static bool IsSystemRole(IReadOnlyList<string> roles)
    {
        return roles.Any(role =>
            string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase));
    }
}
