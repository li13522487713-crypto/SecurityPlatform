using Atlas.Application.LogicFlow.Expressions.Abstractions;
using Atlas.Application.LogicFlow.Expressions.Models;
using Atlas.Application.LogicFlow.Expressions.Repositories;
using Atlas.Core.Exceptions;
using Atlas.Core.Tenancy;
using Atlas.Domain.LogicFlow.Expressions;

namespace Atlas.Infrastructure.LogicFlow.Expressions.Services;

public sealed class RuleChainCommandService : IRuleChainCommandService
{
    private readonly IRuleChainRepository _repo;

    public RuleChainCommandService(IRuleChainRepository repo)
    {
        _repo = repo;
    }

    public async Task<long> CreateAsync(
        RuleChainCreateRequest request,
        TenantId tenantId,
        string operatorName,
        CancellationToken cancellationToken)
    {
        var entity = new RuleChainDefinition(tenantId, request.Name)
        {
            DisplayName = request.DisplayName,
            Description = request.Description,
            StepsJson = request.StepsJson,
            DefaultOutputExpression = request.DefaultOutputExpression,
            SortOrder = request.SortOrder,
            CreatedBy = operatorName,
        };
        return await _repo.InsertAsync(entity, cancellationToken);
    }

    public async Task UpdateAsync(
        RuleChainUpdateRequest request,
        TenantId tenantId,
        string operatorName,
        CancellationToken cancellationToken)
    {
        var entity = await _repo.GetByIdAsync(request.Id, tenantId, cancellationToken)
            ?? throw new BusinessException("NOT_FOUND", $"规则链 {request.Id} 不存在");

        entity.Name = request.Name;
        entity.DisplayName = request.DisplayName;
        entity.Description = request.Description;
        entity.StepsJson = request.StepsJson;
        entity.DefaultOutputExpression = request.DefaultOutputExpression;
        entity.IsEnabled = request.IsEnabled;
        entity.SortOrder = request.SortOrder;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = operatorName;

        await _repo.UpdateAsync(entity, cancellationToken);
    }

    public async Task DeleteAsync(long id, TenantId tenantId, CancellationToken cancellationToken)
    {
        _ = await _repo.GetByIdAsync(id, tenantId, cancellationToken)
            ?? throw new BusinessException("NOT_FOUND", $"规则链 {id} 不存在");
        await _repo.DeleteAsync(id, tenantId, cancellationToken);
    }
}
