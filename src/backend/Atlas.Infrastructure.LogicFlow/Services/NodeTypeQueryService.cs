using Atlas.Application.LogicFlow.Nodes.Abstractions;
using Atlas.Application.LogicFlow.Nodes.Models;
using Atlas.Application.LogicFlow.Nodes.Repositories;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.LogicFlow.Nodes;
using AutoMapper;

namespace Atlas.Infrastructure.LogicFlow.Services;

public sealed class NodeTypeQueryService : INodeTypeQueryService
{
    private readonly INodeTypeRepository _repository;
    private readonly IMapper _mapper;

    public NodeTypeQueryService(INodeTypeRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<PagedResult<NodeTypeListItem>> QueryAsync(
        NodeTypeQueryRequest request,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var pageIndex = request.PageIndex < 1 ? 1 : request.PageIndex;
        var pageSize = request.PageSize < 1 ? 10 : request.PageSize;

        var (items, total) = await _repository.QueryPageAsync(
            pageIndex, pageSize,
            tenantId,
            request.Keyword, request.Category, request.IsBuiltIn,
            cancellationToken);

        var result = items.Select(x => _mapper.Map<NodeTypeListItem>(x)).ToArray();
        return new PagedResult<NodeTypeListItem>(result, total, pageIndex, pageSize);
    }

    public async Task<NodeTypeDetailResponse?> GetByIdAsync(long id, TenantId tenantId, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(id, tenantId, cancellationToken);
        return entity is null ? null : _mapper.Map<NodeTypeDetailResponse>(entity);
    }

    public async Task<NodeTypeDetailResponse?> GetByTypeKeyAsync(string typeKey, TenantId tenantId, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByTypeKeyAsync(typeKey, tenantId, cancellationToken);
        return entity is null ? null : _mapper.Map<NodeTypeDetailResponse>(entity);
    }

    public Task<IReadOnlyList<NodeCategoryInfo>> GetCategoriesAsync(CancellationToken cancellationToken)
    {
        var categories = Enum.GetValues<NodeCategory>()
            .Select(c => new NodeCategoryInfo(c, GetCategoryDisplayName(c), 0))
            .ToList();
        return Task.FromResult<IReadOnlyList<NodeCategoryInfo>>(categories);
    }

    private static string GetCategoryDisplayName(NodeCategory category) => category switch
    {
        NodeCategory.Trigger => "触发",
        NodeCategory.DataRead => "数据读取",
        NodeCategory.DataTransform => "数据变换",
        NodeCategory.ControlFlow => "控制流",
        NodeCategory.Transaction => "事务与可靠性",
        NodeCategory.SystemIntegration => "系统联动",
        _ => category.ToString(),
    };
}
