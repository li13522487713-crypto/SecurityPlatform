using Atlas.Application.Abstractions;
using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Identity.Repositories;
using Atlas.Application.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Infrastructure.Services;

public sealed class AuthProfileService : IAuthProfileService
{
    private readonly IUserAccountRepository _userRepository;
    private readonly IRbacResolver _rbacResolver;

    public AuthProfileService(
        IUserAccountRepository userRepository,
        IRbacResolver rbacResolver)
    {
        _userRepository = userRepository;
        _rbacResolver = rbacResolver;
    }

    public async Task<AuthProfileResult?> GetProfileAsync(
        long userId,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var account = await _userRepository.FindByIdAsync(tenantId, userId, cancellationToken);
        if (account is null)
        {
            return null;
        }

        var roleCodes = await _rbacResolver.GetRoleCodesAsync(account, tenantId, cancellationToken);
        var permissionCodes = await _rbacResolver.GetPermissionCodesAsync(tenantId, userId, cancellationToken);

        return new AuthProfileResult(
            account.Id.ToString(),
            account.Username,
            account.DisplayName,
            tenantId.Value.ToString("D"),
            roleCodes,
            permissionCodes,
            null);
    }

    
}
