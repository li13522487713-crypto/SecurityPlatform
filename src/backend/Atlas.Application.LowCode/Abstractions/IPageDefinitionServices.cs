using Atlas.Application.LowCode.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.LowCode.Abstractions;

public interface IPageDefinitionQueryService
{
    Task<IReadOnlyList<PageDefinitionListItem>> ListAsync(TenantId tenantId, long appId, CancellationToken cancellationToken);
    Task<PageDefinitionDetail?> GetAsync(TenantId tenantId, long appId, long id, CancellationToken cancellationToken);
}

public interface IPageDefinitionCommandService
{
    Task<long> CreateAsync(TenantId tenantId, long currentUserId, long appId, PageDefinitionCreateRequest request, CancellationToken cancellationToken);
    Task UpdateAsync(TenantId tenantId, long currentUserId, long appId, long id, PageDefinitionUpdateRequest request, CancellationToken cancellationToken);
    Task ReplaceSchemaAsync(TenantId tenantId, long currentUserId, long appId, long id, PageSchemaReplaceRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(TenantId tenantId, long currentUserId, long appId, long id, CancellationToken cancellationToken);
    Task ReorderAsync(TenantId tenantId, long currentUserId, long appId, PagesReorderRequest request, CancellationToken cancellationToken);
}

public interface IAppVariableQueryService
{
    Task<IReadOnlyList<AppVariableDto>> ListAsync(TenantId tenantId, long appId, string? scope, CancellationToken cancellationToken);
}

public interface IAppVariableCommandService
{
    Task<long> CreateAsync(TenantId tenantId, long currentUserId, long appId, AppVariableCreateRequest request, CancellationToken cancellationToken);
    Task UpdateAsync(TenantId tenantId, long currentUserId, long appId, long id, AppVariableUpdateRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(TenantId tenantId, long currentUserId, long appId, long id, CancellationToken cancellationToken);
}
