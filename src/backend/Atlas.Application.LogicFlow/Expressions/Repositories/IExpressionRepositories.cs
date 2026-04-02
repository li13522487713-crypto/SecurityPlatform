using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.LogicFlow.Expressions;

namespace Atlas.Application.LogicFlow.Expressions.Repositories;

public interface IFunctionDefinitionRepository
{
    Task<FunctionDefinition?> GetByIdAsync(long id, TenantId tenantId, CancellationToken cancellationToken);
    Task<FunctionDefinition?> GetByNameAsync(string name, TenantId tenantId, CancellationToken cancellationToken);
    Task<PagedResult<FunctionDefinition>> GetPagedAsync(int pageIndex, int pageSize, TenantId tenantId, string? keyword, int? category, CancellationToken cancellationToken);
    Task<List<FunctionDefinition>> GetAllAsync(TenantId tenantId, CancellationToken cancellationToken);
    Task<long> InsertAsync(FunctionDefinition entity, CancellationToken cancellationToken);
    Task UpdateAsync(FunctionDefinition entity, CancellationToken cancellationToken);
    Task DeleteAsync(long id, TenantId tenantId, CancellationToken cancellationToken);
}

public interface IDecisionTableRepository
{
    Task<DecisionTableDefinition?> GetByIdAsync(long id, TenantId tenantId, CancellationToken cancellationToken);
    Task<PagedResult<DecisionTableDefinition>> GetPagedAsync(int pageIndex, int pageSize, TenantId tenantId, string? keyword, CancellationToken cancellationToken);
    Task<long> InsertAsync(DecisionTableDefinition entity, CancellationToken cancellationToken);
    Task UpdateAsync(DecisionTableDefinition entity, CancellationToken cancellationToken);
    Task DeleteAsync(long id, TenantId tenantId, CancellationToken cancellationToken);
}

public interface IRuleChainRepository
{
    Task<RuleChainDefinition?> GetByIdAsync(long id, TenantId tenantId, CancellationToken cancellationToken);
    Task<PagedResult<RuleChainDefinition>> GetPagedAsync(int pageIndex, int pageSize, TenantId tenantId, string? keyword, CancellationToken cancellationToken);
    Task<long> InsertAsync(RuleChainDefinition entity, CancellationToken cancellationToken);
    Task UpdateAsync(RuleChainDefinition entity, CancellationToken cancellationToken);
    Task DeleteAsync(long id, TenantId tenantId, CancellationToken cancellationToken);
}
