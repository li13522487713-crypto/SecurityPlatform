using System.Threading;
using System.Threading.Tasks;
using Atlas.Core.Tenancy;

namespace Atlas.Application.Authorization;

/// <summary>
/// 治理 M-G03-C2：资源访问统一守卫。
///
/// 三级合并判定（按优先级合并）：
/// 1. **平台 RBAC**：用户是平台管理员（IsPlatformAdmin / system:admin / *:*:*）→ 允许所有。
/// 2. **工作空间角色**：用户是 workspace member 且 workspace role 的 DefaultActionsJson 含 action → 允许。
/// 3. **资源 ACL**：当 ResourceId 非空时，按 (workspaceRoleId, resourceType, resourceId) 查 WorkspaceResourcePermission，
///    其 ActionsJson 优先级高于 workspace 角色默认 actions（覆盖）。
///
/// 这里只做"是否允许"判定；具体 action 编码（view/edit/delete/manage-permission/...) 由调用方约定。
/// </summary>
public interface IResourceAccessGuard
{
    /// <summary>检查访问决策；不抛异常，返回 <see cref="ResourceAccessDecision"/>。</summary>
    Task<ResourceAccessDecision> CheckAsync(ResourceAccessQuery query, CancellationToken cancellationToken);

    /// <summary>失败时抛 BusinessException(NotFound|Forbidden)。NotFound 用于 workspace 不存在；其它情况用 Forbidden。</summary>
    Task RequireAsync(ResourceAccessQuery query, CancellationToken cancellationToken);
}

public sealed record ResourceAccessQuery(
    TenantId TenantId,
    long UserId,
    bool IsPlatformAdmin,
    long WorkspaceId,
    string ResourceType,
    long? ResourceId,
    string Action);

public sealed record ResourceAccessDecision(
    bool Allowed,
    string? DeniedReason,
    /// <summary>granting tier：platform / workspace / resource。Allowed=true 时必有值。</summary>
    string? GrantingTier);
