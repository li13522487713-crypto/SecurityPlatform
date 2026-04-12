using Atlas.Application.Identity.Abstractions;
using Atlas.Core.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Atlas.Presentation.Shared.Authorization;

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IPermissionDecisionService _permissionDecisionService;
    private readonly IAppContextAccessor _appContextAccessor;

    public PermissionAuthorizationHandler(
        ICurrentUserAccessor currentUserAccessor,
        IPermissionDecisionService permissionDecisionService,
        IAppContextAccessor appContextAccessor)
    {
        _currentUserAccessor = currentUserAccessor;
        _permissionDecisionService = permissionDecisionService;
        _appContextAccessor = appContextAccessor;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return;
        }

        // 对受信任签名令牌中的平台管理员声明走快速放行，避免每个请求都触发数据库权限决策。
        var appId = _appContextAccessor.ResolveAppId();
        if (currentUser.IsPlatformAdmin && appId is null)
        {
            context.Succeed(requirement);
            return;
        }

        var cancellationToken = (context.Resource as HttpContext)?.RequestAborted ?? CancellationToken.None;
        var hasPermission = await _permissionDecisionService.HasPermissionAsync(
            currentUser.TenantId,
            currentUser.UserId,
            requirement.PermissionCode,
            cancellationToken);
        if (hasPermission)
        {
            context.Succeed(requirement);
        }
    }
}
