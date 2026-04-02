using Atlas.Application.LogicFlow.Expressions.Abstractions;
using Atlas.Application.LogicFlow.Expressions.Models;
using Atlas.Application.LogicFlow.Expressions.Repositories;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using AutoMapper;

namespace Atlas.Infrastructure.LogicFlow.Expressions.Services;

public sealed class FunctionDefinitionQueryService : IFunctionDefinitionQueryService
{
    private readonly IFunctionDefinitionRepository _repo;
    private readonly IMapper _mapper;

    public FunctionDefinitionQueryService(IFunctionDefinitionRepository repo, IMapper mapper)
    {
        _repo = repo;
        _mapper = mapper;
    }

    public async Task<FunctionDefinitionResponse?> GetByIdAsync(long id, TenantId tenantId, CancellationToken cancellationToken)
    {
        var entity = await _repo.GetByIdAsync(id, tenantId, cancellationToken);
        return entity == null ? null : _mapper.Map<FunctionDefinitionResponse>(entity);
    }

    public async Task<PagedResult<FunctionDefinitionListItem>> GetPagedAsync(
        PagedRequest request,
        TenantId tenantId,
        string? keyword = null,
        int? category = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _repo.GetPagedAsync(request.PageIndex, request.PageSize, tenantId, keyword, category, cancellationToken);
        return new PagedResult<FunctionDefinitionListItem>(
            _mapper.Map<List<FunctionDefinitionListItem>>(result.Items),
            result.Total, result.PageIndex, result.PageSize);
    }

    public async Task<IReadOnlyList<FunctionDefinitionListItem>> GetAllAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        var list = await _repo.GetAllAsync(tenantId, cancellationToken);
        return _mapper.Map<List<FunctionDefinitionListItem>>(list);
    }
}
