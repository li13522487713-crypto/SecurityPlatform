using Atlas.Application.LogicFlow.Nodes.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.LogicFlow.Nodes.Abstractions;

public interface INodeTemplateCommandService
{
    Task<long> CreateAsync(
        NodeTemplateCreateRequest request,
        TenantId tenantId,
        CancellationToken cancellationToken);

    Task UpdateAsync(
        long id,
        NodeTemplateUpdateRequest request,
        CancellationToken cancellationToken);

    Task DeleteAsync(long id, CancellationToken cancellationToken);
}
