using Atlas.Application.LogicFlow.Nodes.Abstractions;
using Atlas.Application.LogicFlow.Nodes.Models;
using Atlas.Application.LogicFlow.Nodes.Repositories;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using AutoMapper;

namespace Atlas.Infrastructure.LogicFlow.Services;

public sealed class NodeTemplateQueryService : INodeTemplateQueryService
{
    private readonly INodeTemplateRepository _repository;
    private readonly IMapper _mapper;

    public NodeTemplateQueryService(INodeTemplateRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<PagedResult<NodeTemplateListItem>> QueryAsync(
        NodeTemplateQueryRequest request,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var pageIndex = request.PageIndex < 1 ? 1 : request.PageIndex;
        var pageSize = request.PageSize < 1 ? 10 : request.PageSize;

        var (items, total) = await _repository.QueryPageAsync(
            pageIndex, pageSize,
            tenantId,
            request.Keyword, request.Category,
            cancellationToken);

        var result = items.Select(x => _mapper.Map<NodeTemplateListItem>(x)).ToArray();
        return new PagedResult<NodeTemplateListItem>(result, total, pageIndex, pageSize);
    }

    public async Task<NodeTemplateDetailResponse?> GetByIdAsync(long id, TenantId tenantId, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(id, tenantId, cancellationToken);
        return entity is null ? null : _mapper.Map<NodeTemplateDetailResponse>(entity);
    }
}
