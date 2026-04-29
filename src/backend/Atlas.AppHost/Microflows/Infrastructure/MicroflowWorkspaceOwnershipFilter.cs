using System.Globalization;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Repositories;
using Atlas.Application.Platform.Abstractions;
using Atlas.Core.Identity;
using Atlas.Core.Tenancy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Atlas.AppHost.Microflows.Infrastructure;

public sealed class MicroflowWorkspaceOwnershipFilter : IAsyncAuthorizationFilter
{
    private readonly IMicroflowRequestContextAccessor _requestContextAccessor;
    private readonly IMicroflowResourceRepository _resourceRepository;
    private readonly IWorkspacePortalService _workspacePortalService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public MicroflowWorkspaceOwnershipFilter(
        IMicroflowRequestContextAccessor requestContextAccessor,
        IMicroflowResourceRepository resourceRepository,
        IWorkspacePortalService workspacePortalService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor)
    {
        _requestContextAccessor = requestContextAccessor;
        _resourceRepository = resourceRepository;
        _workspacePortalService = workspacePortalService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var traceId = _requestContextAccessor.Current.TraceId;
        if (context.HttpContext.User.Identity?.IsAuthenticated != true)
        {
            Reject(context, StatusCodes.Status401Unauthorized, MicroflowApiErrorCode.MicroflowUnauthorized, "微流 API 需要登录后访问。", traceId);
            return;
        }

        var workspaceId = await ResolveWorkspaceIdAsync(context, context.HttpContext.RequestAborted);
        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            Reject(context, StatusCodes.Status403Forbidden, MicroflowApiErrorCode.MicroflowWorkspaceForbidden, "缺少工作区上下文，无法访问微流资源。", traceId);
            return;
        }

        if (!long.TryParse(workspaceId, out var workspaceIdValue))
        {
            Reject(context, StatusCodes.Status403Forbidden, MicroflowApiErrorCode.MicroflowWorkspaceForbidden, "工作区 ID 格式无效。", traceId);
            return;
        }

        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        var tenantId = _tenantProvider.GetTenantId();
        var workspace = await _workspacePortalService.GetWorkspaceAsync(
            tenantId,
            workspaceIdValue,
            currentUser.UserId,
            currentUser.IsPlatformAdmin,
            context.HttpContext.RequestAborted);

        if (workspace is null)
        {
            Reject(context, StatusCodes.Status403Forbidden, MicroflowApiErrorCode.MicroflowWorkspaceForbidden, "当前账号无权访问该工作区的微流资源。", traceId);
        }
    }

    private async Task<string?> ResolveWorkspaceIdAsync(AuthorizationFilterContext context, CancellationToken cancellationToken)
    {
        var current = _requestContextAccessor.Current;
        var queryWorkspaceId = context.HttpContext.Request.Query["workspaceId"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(queryWorkspaceId))
        {
            return queryWorkspaceId;
        }

        if (!string.IsNullOrWhiteSpace(current.WorkspaceId))
        {
            return current.WorkspaceId;
        }

        if (context.RouteData.Values.TryGetValue("id", out var rawId) && rawId is not null)
        {
            var id = Convert.ToString(rawId);
            if (!string.IsNullOrWhiteSpace(id))
            {
                var resource = await _resourceRepository.GetByIdAsync(id, cancellationToken);
                return resource?.WorkspaceId;
            }
        }

        if (context.RouteData.Values.TryGetValue("appId", out var rawAppId) && rawAppId is not null)
        {
            var appId = Convert.ToString(rawAppId);
            if (!string.IsNullOrWhiteSpace(appId))
            {
                var workspaceId = await ResolveWorkspaceIdFromAppIdAsync(appId, cancellationToken);
                if (!string.IsNullOrWhiteSpace(workspaceId))
                {
                    return workspaceId;
                }
            }
        }

        return null;
    }

    private async Task<string?> ResolveWorkspaceIdFromAppIdAsync(string appId, CancellationToken cancellationToken)
    {
        var current = _requestContextAccessor.Current;
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return null;
        }

        var tenantId = _tenantProvider.GetTenantId();
        var workspaceByAppKey = await _workspacePortalService.GetWorkspaceByAppKeyAsync(
            tenantId,
            appId,
            currentUser.UserId,
            currentUser.IsPlatformAdmin,
            cancellationToken);
        if (workspaceByAppKey is not null)
        {
            return workspaceByAppKey.Id.ToString(CultureInfo.InvariantCulture);
        }

        if (!long.TryParse(appId, CultureInfo.InvariantCulture, out var appIdValue))
        {
            return null;
        }

        var candidateWorkspaceId = string.IsNullOrWhiteSpace(current.WorkspaceId)
            ? appIdValue
            : long.TryParse(current.WorkspaceId, CultureInfo.InvariantCulture, out var currentWorkspaceId)
                ? currentWorkspaceId
                : appIdValue;
        var workspace = await _workspacePortalService.GetWorkspaceAsync(
            tenantId,
            candidateWorkspaceId,
            currentUser.UserId,
            currentUser.IsPlatformAdmin,
            cancellationToken);
        return workspace is null
            ? null
            : workspace.Id.ToString(CultureInfo.InvariantCulture);
    }

    private static void Reject(AuthorizationFilterContext context, int statusCode, string code, string message, string traceId)
    {
        context.HttpContext.Response.Headers["X-Trace-Id"] = traceId;
        context.Result = new ObjectResult(MicroflowApiResponse<object>.Fail(new MicroflowApiError
        {
            Code = code,
            Message = message,
            HttpStatus = statusCode,
            TraceId = traceId
        }, traceId))
        {
            StatusCode = statusCode
        };
    }
}
