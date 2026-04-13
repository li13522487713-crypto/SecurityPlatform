using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Platform.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class ConversationOwnerResolver : IConversationOwnerResolver
{
    private readonly IAppContextAccessor _appContextAccessor;
    private readonly ITenantDataScopeFilter _tenantDataScopeFilter;
    private readonly IAppDataScopeFilter _appDataScopeFilter;

    public ConversationOwnerResolver(
        IAppContextAccessor appContextAccessor,
        ITenantDataScopeFilter tenantDataScopeFilter,
        IAppDataScopeFilter appDataScopeFilter)
    {
        _appContextAccessor = appContextAccessor;
        _tenantDataScopeFilter = tenantDataScopeFilter;
        _appDataScopeFilter = appDataScopeFilter;
    }

    public async Task<long> ResolveAsync(
        TenantId tenantId,
        long userId,
        CancellationToken cancellationToken)
    {
        var ownerFilter = await ResolveOwnerFilterIdAsync(tenantId, cancellationToken);
        if (ownerFilter.HasValue && userId != ownerFilter.Value)
        {
            throw new BusinessException("NoPermissionAccessConversation", ErrorCodes.Forbidden);
        }

        return ownerFilter ?? userId;
    }

    private async Task<long?> ResolveOwnerFilterIdAsync(
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var appId = _appContextAccessor.ResolveAppId();
        if (appId is > 0)
        {
            return await _appDataScopeFilter.GetOwnerFilterIdAsync(appId.Value, cancellationToken);
        }

        return await _tenantDataScopeFilter.GetOwnerFilterIdAsync(cancellationToken);
    }
}
