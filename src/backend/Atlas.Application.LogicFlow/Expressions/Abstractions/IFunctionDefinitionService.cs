using Atlas.Application.LogicFlow.Expressions.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.LogicFlow.Expressions.Abstractions;

public interface IFunctionDefinitionQueryService
{
    Task<FunctionDefinitionResponse?> GetByIdAsync(long id, TenantId tenantId, CancellationToken cancellationToken);
    Task<PagedResult<FunctionDefinitionListItem>> GetPagedAsync(PagedRequest request, TenantId tenantId, string? keyword = null, int? category = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FunctionDefinitionListItem>> GetAllAsync(TenantId tenantId, CancellationToken cancellationToken);
}

public interface IFunctionDefinitionCommandService
{
    Task<long> CreateAsync(FunctionDefinitionCreateRequest request, TenantId tenantId, string operatorName, CancellationToken cancellationToken);
    Task UpdateAsync(FunctionDefinitionUpdateRequest request, TenantId tenantId, string operatorName, CancellationToken cancellationToken);
    Task DeleteAsync(long id, TenantId tenantId, CancellationToken cancellationToken);
}
