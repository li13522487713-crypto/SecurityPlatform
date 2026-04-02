using Atlas.Application.LogicFlow.Nodes.Abstractions;
using Atlas.Application.LogicFlow.Nodes.Models;
using Atlas.Application.LogicFlow.Nodes.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Tenancy;
using Atlas.Domain.LogicFlow.Nodes;

namespace Atlas.Infrastructure.LogicFlow.Services;

public sealed class NodeTemplateCommandService : INodeTemplateCommandService
{
    private readonly INodeTemplateRepository _repository;
    private readonly IIdGeneratorAccessor _idGen;
    private readonly ITenantProvider _tenantProvider;

    public NodeTemplateCommandService(
        INodeTemplateRepository repository,
        IIdGeneratorAccessor idGen,
        ITenantProvider tenantProvider)
    {
        _repository = repository;
        _idGen = idGen;
        _tenantProvider = tenantProvider;
    }

    public async Task<long> CreateAsync(
        NodeTemplateCreateRequest request,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var id = _idGen.NextId();
        var entity = new NodeTemplate(tenantId, id, request.Name, request.NodeTypeKey);
        entity.Update(request.Name, request.Description, request.PresetConfig, request.Tags, request.IsPublic);

        return await _repository.AddAsync(entity, cancellationToken);
    }

    public async Task UpdateAsync(
        long id,
        NodeTemplateUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(id, _tenantProvider.GetTenantId(), cancellationToken)
            ?? throw new BusinessException("NOT_FOUND", "节点模板不存在");

        entity.Update(request.Name, request.Description, request.PresetConfig, request.Tags, request.IsPublic);
        await _repository.UpdateAsync(entity, cancellationToken);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken)
    {
        if (!await _repository.DeleteAsync(id, cancellationToken))
        {
            throw new BusinessException("NOT_FOUND", "节点模板不存在");
        }
    }
}
