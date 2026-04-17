using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Entities;

namespace Atlas.Application.LowCode.Repositories;

/// <summary>
/// 应用定义仓储抽象（M01）。
/// 所有方法严格遵守 AGENTS.md "禁止循环内 DB 操作" 约束。
/// </summary>
public interface IAppDefinitionRepository
{
    Task<long> InsertAsync(AppDefinition app, CancellationToken cancellationToken);

    Task<bool> UpdateAsync(AppDefinition app, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken);

    Task<AppDefinition?> FindByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken);

    Task<AppDefinition?> FindByCodeAsync(TenantId tenantId, string code, CancellationToken cancellationToken);

    Task<bool> ExistsCodeAsync(TenantId tenantId, string code, long? excludeId, CancellationToken cancellationToken);

    Task<(IReadOnlyList<AppDefinition> Items, int TotalCount)> QueryPagedAsync(
        TenantId tenantId,
        int pageIndex,
        int pageSize,
        string? keyword,
        string? status,
        CancellationToken cancellationToken);
}

/// <summary>页面仓储抽象（M01）。</summary>
public interface IPageDefinitionRepository
{
    Task<long> InsertAsync(PageDefinition page, CancellationToken cancellationToken);

    Task<bool> UpdateAsync(PageDefinition page, CancellationToken cancellationToken);

    Task<int> ReorderBatchAsync(TenantId tenantId, long appId, IReadOnlyDictionary<long, int> idToOrder, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(TenantId tenantId, long appId, long id, CancellationToken cancellationToken);

    Task<PageDefinition?> FindByIdAsync(TenantId tenantId, long appId, long id, CancellationToken cancellationToken);

    Task<bool> ExistsCodeAsync(TenantId tenantId, long appId, string code, long? excludeId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PageDefinition>> ListByAppAsync(TenantId tenantId, long appId, CancellationToken cancellationToken);
}

/// <summary>变量仓储抽象（M01）。</summary>
public interface IAppVariableRepository
{
    Task<long> InsertAsync(AppVariable variable, CancellationToken cancellationToken);

    Task<bool> UpdateAsync(AppVariable variable, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(TenantId tenantId, long appId, long id, CancellationToken cancellationToken);

    Task<AppVariable?> FindByIdAsync(TenantId tenantId, long appId, long id, CancellationToken cancellationToken);

    Task<bool> ExistsCodeAsync(TenantId tenantId, long appId, string code, long? excludeId, CancellationToken cancellationToken);

    Task<IReadOnlyList<AppVariable>> ListByAppAsync(TenantId tenantId, long appId, string? scope, CancellationToken cancellationToken);
}

/// <summary>内容参数仓储抽象（M01）。</summary>
public interface IAppContentParamRepository
{
    Task<long> InsertAsync(AppContentParam param, CancellationToken cancellationToken);

    Task<bool> UpdateAsync(AppContentParam param, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(TenantId tenantId, long appId, long id, CancellationToken cancellationToken);

    Task<AppContentParam?> FindByIdAsync(TenantId tenantId, long appId, long id, CancellationToken cancellationToken);

    Task<bool> ExistsCodeAsync(TenantId tenantId, long appId, string code, long? excludeId, CancellationToken cancellationToken);

    Task<IReadOnlyList<AppContentParam>> ListByAppAsync(TenantId tenantId, long appId, CancellationToken cancellationToken);
}

/// <summary>版本归档仓储抽象（M01；M14 / M16 进一步使用）。</summary>
public interface IAppVersionArchiveRepository
{
    Task<long> InsertAsync(AppVersionArchive archive, CancellationToken cancellationToken);

    Task<AppVersionArchive?> FindByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken);

    Task<IReadOnlyList<AppVersionArchive>> ListByAppAsync(TenantId tenantId, long appId, bool includeSystemSnapshot, CancellationToken cancellationToken);
}

/// <summary>发布产物仓储抽象（M01；M17 完整使用）。</summary>
public interface IAppPublishArtifactRepository
{
    Task<long> InsertAsync(AppPublishArtifact artifact, CancellationToken cancellationToken);

    Task<bool> UpdateAsync(AppPublishArtifact artifact, CancellationToken cancellationToken);

    Task<AppPublishArtifact?> FindByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken);

    Task<IReadOnlyList<AppPublishArtifact>> ListByAppAsync(TenantId tenantId, long appId, CancellationToken cancellationToken);
}

/// <summary>资源引用反查仓储抽象（M01；M14 完整使用）。</summary>
public interface IAppResourceReferenceRepository
{
    Task<int> ReplaceForAppAsync(TenantId tenantId, long appId, IReadOnlyList<AppResourceReference> references, CancellationToken cancellationToken);

    Task<IReadOnlyList<AppResourceReference>> ListByResourceAsync(TenantId tenantId, string resourceType, string resourceId, CancellationToken cancellationToken);

    Task<IReadOnlyList<AppResourceReference>> ListByAppAsync(TenantId tenantId, long appId, CancellationToken cancellationToken);
}
