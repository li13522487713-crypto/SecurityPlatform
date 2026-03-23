using Atlas.Application.System.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.System.Abstractions;

public interface ISystemConfigQueryService
{
    Task<PagedResult<SystemConfigDto>> GetSystemConfigsPagedAsync(
        TenantId tenantId,
        string? keyword,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken);

    Task<SystemConfigDto?> GetByKeyAsync(
        TenantId tenantId,
        string configKey,
        string? appId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SystemConfigDto>> ListSystemConfigsAsync(
        TenantId tenantId,
        string? groupName,
        string? appId,
        IReadOnlyCollection<string>? keys,
        CancellationToken cancellationToken);

    /// <summary>获取所有 FeatureFlag 类型的配置（用于前端 useFeatureFlag composable）</summary>
    Task<IReadOnlyList<SystemConfigDto>> GetFeatureFlagsAsync(
        TenantId tenantId,
        CancellationToken cancellationToken);
}
