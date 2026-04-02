using Atlas.Application.LogicFlow.Expressions.Abstractions;
using Atlas.Application.LogicFlow.Expressions.Models;
using Atlas.Application.LogicFlow.Expressions.Repositories;
using Atlas.Core.Exceptions;
using Atlas.Core.Tenancy;
using Atlas.Domain.LogicFlow.Expressions;

namespace Atlas.Infrastructure.LogicFlow.Expressions.Services;

public sealed class DecisionTableCommandService : IDecisionTableCommandService
{
    private readonly IDecisionTableRepository _repo;

    public DecisionTableCommandService(IDecisionTableRepository repo)
    {
        _repo = repo;
    }

    public async Task<long> CreateAsync(
        DecisionTableCreateRequest request,
        TenantId tenantId,
        string operatorName,
        CancellationToken cancellationToken)
    {
        var entity = new DecisionTableDefinition(tenantId, request.Name)
        {
            DisplayName = request.DisplayName,
            Description = request.Description,
            HitPolicy = request.HitPolicy,
            InputColumnsJson = request.InputColumnsJson,
            OutputColumnsJson = request.OutputColumnsJson,
            RowsJson = request.RowsJson,
            SortOrder = request.SortOrder,
            CreatedBy = operatorName,
        };
        return await _repo.InsertAsync(entity, cancellationToken);
    }

    public async Task UpdateAsync(
        DecisionTableUpdateRequest request,
        TenantId tenantId,
        string operatorName,
        CancellationToken cancellationToken)
    {
        var entity = await _repo.GetByIdAsync(request.Id, tenantId, cancellationToken)
            ?? throw new BusinessException("NOT_FOUND", $"决策表 {request.Id} 不存在");

        entity.Name = request.Name;
        entity.DisplayName = request.DisplayName;
        entity.Description = request.Description;
        entity.HitPolicy = request.HitPolicy;
        entity.InputColumnsJson = request.InputColumnsJson;
        entity.OutputColumnsJson = request.OutputColumnsJson;
        entity.RowsJson = request.RowsJson;
        entity.IsEnabled = request.IsEnabled;
        entity.SortOrder = request.SortOrder;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = operatorName;

        await _repo.UpdateAsync(entity, cancellationToken);
    }

    public async Task DeleteAsync(long id, TenantId tenantId, CancellationToken cancellationToken)
    {
        _ = await _repo.GetByIdAsync(id, tenantId, cancellationToken)
            ?? throw new BusinessException("NOT_FOUND", $"决策表 {id} 不存在");
        await _repo.DeleteAsync(id, tenantId, cancellationToken);
    }
}
