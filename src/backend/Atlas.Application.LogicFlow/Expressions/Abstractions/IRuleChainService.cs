using Atlas.Application.LogicFlow.Expressions.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.LogicFlow.Expressions.Abstractions;

public interface IRuleChainQueryService
{
    Task<RuleChainResponse?> GetByIdAsync(long id, TenantId tenantId, CancellationToken cancellationToken);
    Task<PagedResult<RuleChainListItem>> GetPagedAsync(PagedRequest request, TenantId tenantId, string? keyword, CancellationToken cancellationToken);
    Task<RuleChainExecuteResponse> ExecuteAsync(RuleChainExecuteRequest request, TenantId tenantId, CancellationToken cancellationToken);
}

public interface IRuleChainCommandService
{
    Task<long> CreateAsync(RuleChainCreateRequest request, TenantId tenantId, string operatorName, CancellationToken cancellationToken);
    Task UpdateAsync(RuleChainUpdateRequest request, TenantId tenantId, string operatorName, CancellationToken cancellationToken);
    Task DeleteAsync(long id, TenantId tenantId, CancellationToken cancellationToken);
}
