using Atlas.Application.Identity.Abstractions;
using Atlas.Core.Identity;
using Microsoft.AspNetCore.Authorization;

namespace Atlas.Presentation.Shared.Authorization;

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IPermissionDecisionService _permissionDecisionService;

    public PermissionAuthorizationHandler(
        ICurrentUserAccessor currentUserAccessor,
        IPermissionDecisionService permissionDecisionService)
    {
        _currentUserAccessor = currentUserAccessor;
        _permissionDecisionService = permissionDecisionService;
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

        var hasPermission = await _permissionDecisionService.HasPermissionAsync(
            currentUser.TenantId,
            currentUser.UserId,
            requirement.PermissionCode,
            CancellationToken.None);
        if (hasPermission)
        {
            context.Succeed(requirement);
        }
    }
}
