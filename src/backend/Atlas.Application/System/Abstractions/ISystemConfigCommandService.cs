using Atlas.Application.System.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.System.Abstractions;

public interface ISystemConfigCommandService
{
    Task<long> CreateSystemConfigAsync(
        TenantId tenantId,
        SystemConfigCreateRequest request,
        CancellationToken cancellationToken);

    Task UpdateSystemConfigAsync(
        TenantId tenantId,
        long id,
        SystemConfigUpdateRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<long>> BatchUpsertSystemConfigsAsync(
        TenantId tenantId,
        SystemConfigBatchUpsertRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// 删除参数。内置参数（IsBuiltIn=true）不可删除，等保要求禁止删除系统基础配置。
    /// </summary>
    Task DeleteSystemConfigAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken);

    Task DeleteSystemConfigByKeyAsync(
        TenantId tenantId,
        string configKey,
        string? appId,
        CancellationToken cancellationToken);
}
