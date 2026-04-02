using Atlas.Application.LogicFlow.Nodes.Abstractions;
using Atlas.Application.LogicFlow.Nodes.Models;
using Atlas.Application.LogicFlow.Nodes.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Tenancy;
using Atlas.Domain.LogicFlow.Nodes;

namespace Atlas.Infrastructure.LogicFlow.Services;

public sealed class NodeTypeCommandService : INodeTypeCommandService
{
    private readonly INodeTypeRepository _repository;
    private readonly IIdGeneratorAccessor _idGen;
    private readonly ITenantProvider _tenantProvider;

    public NodeTypeCommandService(
        INodeTypeRepository repository,
        IIdGeneratorAccessor idGen,
        ITenantProvider tenantProvider)
    {
        _repository = repository;
        _idGen = idGen;
        _tenantProvider = tenantProvider;
    }

    public async Task<long> CreateAsync(
        NodeTypeCreateRequest request,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        if (await _repository.ExistsByTypeKeyAsync(request.TypeKey, tenantId, cancellationToken))
        {
            throw new BusinessException("NODE_TYPE_EXISTS", $"节点类型 '{request.TypeKey}' 已存在");
        }

        var id = _idGen.NextId();
        var entity = new NodeTypeDefinition(tenantId, id, request.TypeKey, request.Category, request.DisplayName);
        entity.Update(
            request.DisplayName,
            request.Description,
            request.Ports ?? new List<PortDefinition>(),
            request.ConfigSchema,
            request.Capabilities,
            request.UiMetadata);

        return await _repository.AddAsync(entity, cancellationToken);
    }

    public async Task UpdateAsync(
        long id,
        NodeTypeUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(id, _tenantProvider.GetTenantId(), cancellationToken)
            ?? throw new BusinessException("NOT_FOUND", "节点类型不存在");

        if (entity.IsBuiltIn)
        {
            throw new BusinessException("FORBIDDEN", "内置节点类型不可修改");
        }

        entity.Update(
            request.DisplayName,
            request.Description,
            request.Ports ?? new List<PortDefinition>(),
            request.ConfigSchema,
            request.Capabilities,
            request.UiMetadata);

        await _repository.UpdateAsync(entity, cancellationToken);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(id, _tenantProvider.GetTenantId(), cancellationToken)
            ?? throw new BusinessException("NOT_FOUND", "节点类型不存在");

        if (entity.IsBuiltIn)
        {
            throw new BusinessException("FORBIDDEN", "内置节点类型不可删除");
        }

        await _repository.DeleteAsync(id, cancellationToken);
    }
}
