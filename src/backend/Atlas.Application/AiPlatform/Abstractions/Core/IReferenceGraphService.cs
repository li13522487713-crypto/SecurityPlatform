using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions.Core;

public interface IReferenceGraphService
{
    Task<ResourceReferenceGraph> GetResourceReferencesAsync(
        TenantId tenantId,
        string ownerType,
        long ownerId,
        CancellationToken cancellationToken);

    Task<DagWorkflowDependencyDto?> GetWorkflowDependenciesAsync(
        TenantId tenantId,
        long workflowId,
        CancellationToken cancellationToken);
}
