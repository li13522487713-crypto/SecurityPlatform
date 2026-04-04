using Atlas.Core.Tenancy;
using Microsoft.Extensions.AI;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface IChatClientFactory
{
    Task<IChatClient> CreateAsync(
        TenantId tenantId,
        long? modelConfigId,
        string? modelName,
        CancellationToken cancellationToken);
}
