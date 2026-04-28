using Atlas.Application.Audit.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.Audit.Abstractions;

public interface IAuditQueryService
{
    /// <summary>
    /// 查询审计记录。
    /// </summary>
    /// <param name="scope">
    /// 治理 R1-B2：可见性范围。
    /// <c>"mine"</c>（默认，未传时也按 mine）— 通过 <see cref="IResourceVisibilityResolver"/> 过滤
    /// 当前用户不可见的 (ResourceType, ResourceId) 行；platform / system admin 自动 bypass。
    /// <c>"all"</c> — 不做资源可见性过滤；只允许 admin 主动开启，非 admin 在控制器层应该拒绝。
    /// </param>
    Task<PagedResult<AuditListItem>> QueryAuditsAsync(
        PagedRequest request,
        TenantId tenantId,
        string? action,
        string? result,
        CancellationToken cancellationToken,
        string? scope = null);

    Task<PagedResult<AuditListItem>> QueryAuditsByResourceAsync(
        PagedRequest request,
        TenantId tenantId,
        string? actorId,
        string? action,
        string? resourceId,
        DateTimeOffset? fromDate,
        DateTimeOffset? toDate,
        CancellationToken cancellationToken,
        string? scope = null);

    Task<IReadOnlyList<AuditListItem>> ExportAuditsCsvAsync(
        TenantId tenantId,
        string? action,
        string? result,
        DateTimeOffset? fromDate,
        DateTimeOffset? toDate,
        int maxRows,
        CancellationToken cancellationToken,
        string? scope = null);
}