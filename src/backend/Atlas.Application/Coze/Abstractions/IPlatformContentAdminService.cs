using Atlas.Application.Coze.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.Coze.Abstractions;

/// <summary>
/// 运营人员维护平台运营内容（PRD 01 首页 / 社区 / 通用管理 / 模板插件摘要）。
/// </summary>
public interface IPlatformContentAdminService
{
    Task<IReadOnlyList<PlatformContentItemDto>> ListAsync(
        TenantId tenantId,
        string? slot,
        bool onlyActive,
        CancellationToken cancellationToken);

    Task<string> CreateAsync(
        TenantId tenantId,
        PlatformContentCreateRequest request,
        CancellationToken cancellationToken);

    Task UpdateAsync(
        TenantId tenantId,
        string id,
        PlatformContentUpdateRequest request,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        TenantId tenantId,
        string id,
        CancellationToken cancellationToken);
}
