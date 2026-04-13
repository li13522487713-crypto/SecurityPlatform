using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface IConversationOwnerResolver
{
    Task<long> ResolveAsync(
        TenantId tenantId,
        long userId,
        CancellationToken cancellationToken);
}
