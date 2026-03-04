using Atlas.Application.Identity.Abstractions;
using Atlas.Core.Identity;
using Microsoft.AspNetCore.Authorization;

namespace Atlas.WebApi.Authorization;

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IRbacResolver _rbacResolver;

    public PermissionAuthorizationHandler(
        ICurrentUserAccessor currentUserAccessor,
        IRbacResolver rbacResolver)
    {
        _currentUserAccessor = currentUserAccessor;
        _rbacResolver = rbacResolver;
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

        var roleCodes = await _rbacResolver.GetRoleCodesAsync(
            currentUser.TenantId,
            currentUser.UserId,
            CancellationToken.None);
        if (roleCodes.Contains("Admin", StringComparer.OrdinalIgnoreCase))
        {
            context.Succeed(requirement);
            return;
        }

        var permissions = await _rbacResolver.GetPermissionCodesAsync(
            currentUser.TenantId,
            currentUser.UserId,
            CancellationToken.None);

        if (permissions.Contains(requirement.PermissionCode, StringComparer.OrdinalIgnoreCase))
        {
            context.Succeed(requirement);
        }
    }
}
