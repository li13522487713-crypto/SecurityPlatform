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

public sealed class DecisionTableQueryService : IDecisionTableQueryService
{
    private readonly IDecisionTableRepository _repo;
    private readonly IMapper _mapper;
    private readonly DecisionTableExecutor _executor;

    public DecisionTableQueryService(IDecisionTableRepository repo, IMapper mapper, DecisionTableExecutor executor)
    {
        _repo = repo;
        _mapper = mapper;
        _executor = executor;
    }

    public async Task<DecisionTableResponse?> GetByIdAsync(long id, TenantId tenantId, CancellationToken cancellationToken)
    {
        var entity = await _repo.GetByIdAsync(id, tenantId, cancellationToken);
        return entity == null ? null : _mapper.Map<DecisionTableResponse>(entity);
    }

    public async Task<PagedResult<DecisionTableListItem>> GetPagedAsync(
        PagedRequest request,
        TenantId tenantId,
        string? keyword,
        CancellationToken cancellationToken)
    {
        var result = await _repo.GetPagedAsync(request.PageIndex, request.PageSize, tenantId, keyword, cancellationToken);
        return new PagedResult<DecisionTableListItem>(
            _mapper.Map<List<DecisionTableListItem>>(result.Items),
            result.Total, result.PageIndex, result.PageSize);
    }

    public async Task<DecisionTableExecuteResponse> ExecuteAsync(
        DecisionTableExecuteRequest request,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var entity = await _repo.GetByIdAsync(request.TableId, tenantId, cancellationToken)
            ?? throw new Core.Exceptions.BusinessException("NOT_FOUND", $"决策表 {request.TableId} 不存在");

        var model = new DecisionTableModel
        {
            InputColumns = JsonSerializer.Deserialize<List<DecisionInputColumn>>(entity.InputColumnsJson) ?? [],
            OutputColumns = JsonSerializer.Deserialize<List<DecisionOutputColumn>>(entity.OutputColumnsJson) ?? [],
            Rows = JsonSerializer.Deserialize<List<DecisionRow>>(entity.RowsJson) ?? [],
            HitPolicy = entity.HitPolicy,
        };

        var result = _executor.Execute(model, request.Input);
        return new DecisionTableExecuteResponse(result.IsMatched, result.MatchedOutputs);
    }
}
