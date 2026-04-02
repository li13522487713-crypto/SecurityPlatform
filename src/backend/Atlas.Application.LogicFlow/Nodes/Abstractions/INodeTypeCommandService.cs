using Atlas.Application.LogicFlow.Nodes.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.LogicFlow.Nodes.Abstractions;

public interface INodeTypeCommandService
{
    Task<long> CreateAsync(
        NodeTypeCreateRequest request,
        TenantId tenantId,
        CancellationToken cancellationToken);

    Task UpdateAsync(
        long id,
        NodeTypeUpdateRequest request,
        CancellationToken cancellationToken);

    Task DeleteAsync(long id, CancellationToken cancellationToken);
}
