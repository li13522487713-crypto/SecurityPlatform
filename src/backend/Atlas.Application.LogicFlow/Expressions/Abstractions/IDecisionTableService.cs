using Atlas.Application.LogicFlow.Expressions.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.LogicFlow.Expressions.Abstractions;

public interface IDecisionTableQueryService
{
    Task<DecisionTableResponse?> GetByIdAsync(long id, TenantId tenantId, CancellationToken cancellationToken);
    Task<PagedResult<DecisionTableListItem>> GetPagedAsync(PagedRequest request, TenantId tenantId, string? keyword, CancellationToken cancellationToken);
    Task<DecisionTableExecuteResponse> ExecuteAsync(DecisionTableExecuteRequest request, TenantId tenantId, CancellationToken cancellationToken);
}

public interface IDecisionTableCommandService
{
    Task<long> CreateAsync(DecisionTableCreateRequest request, TenantId tenantId, string operatorName, CancellationToken cancellationToken);
    Task UpdateAsync(DecisionTableUpdateRequest request, TenantId tenantId, string operatorName, CancellationToken cancellationToken);
    Task DeleteAsync(long id, TenantId tenantId, CancellationToken cancellationToken);
}
