using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions.Core;

public interface IConnectorCatalogService
{
    Task<IReadOnlyList<ConnectorCatalogItem>> GetCatalogAsync(
        TenantId tenantId,
        string? resourceType,
        CancellationToken cancellationToken);
}
