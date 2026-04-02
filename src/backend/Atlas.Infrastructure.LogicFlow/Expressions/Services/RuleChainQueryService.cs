using System.Text.Json;
using Atlas.Application.LogicFlow.Expressions.Abstractions;
using Atlas.Application.LogicFlow.Expressions.Models;
using Atlas.Application.LogicFlow.Expressions.Repositories;
using Atlas.Core.Expressions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Infrastructure.LogicFlow.Expressions.Rules;
using AutoMapper;

namespace Atlas.Infrastructure.LogicFlow.Expressions.Services;

public sealed class RuleChainQueryService : IRuleChainQueryService
{
    private readonly IRuleChainRepository _repo;
    private readonly IMapper _mapper;
    private readonly RuleChainExecutor _executor;

    public RuleChainQueryService(IRuleChainRepository repo, IMapper mapper, RuleChainExecutor executor)
    {
        _repo = repo;
        _mapper = mapper;
        _executor = executor;
    }

    public async Task<RuleChainResponse?> GetByIdAsync(long id, TenantId tenantId, CancellationToken cancellationToken)
    {
        var entity = await _repo.GetByIdAsync(id, tenantId, cancellationToken);
        return entity == null ? null : _mapper.Map<RuleChainResponse>(entity);
    }

    public async Task<PagedResult<RuleChainListItem>> GetPagedAsync(
        PagedRequest request,
        TenantId tenantId,
        string? keyword,
        CancellationToken cancellationToken)
    {
        var result = await _repo.GetPagedAsync(request.PageIndex, request.PageSize, tenantId, keyword, cancellationToken);
        return new PagedResult<RuleChainListItem>(
            _mapper.Map<List<RuleChainListItem>>(result.Items),
            result.Total, result.PageIndex, result.PageSize);
    }

    public async Task<RuleChainExecuteResponse> ExecuteAsync(
        RuleChainExecuteRequest request,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var entity = await _repo.GetByIdAsync(request.ChainId, tenantId, cancellationToken)
            ?? throw new Core.Exceptions.BusinessException("NOT_FOUND", $"规则链 {request.ChainId} 不存在");

        var model = new RuleChainModel
        {
            Steps = JsonSerializer.Deserialize<List<RuleStep>>(entity.StepsJson) ?? [],
            DefaultOutputExpression = entity.DefaultOutputExpression,
        };

        var result = _executor.Execute(model, request.Input);
        return new RuleChainExecuteResponse(result.IsMatched, result.Output, result.MatchedStepIndex);
    }
}
