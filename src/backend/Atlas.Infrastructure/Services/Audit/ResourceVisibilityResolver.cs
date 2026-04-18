using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Atlas.Application.Audit.Abstractions;
using Atlas.Application.Authorization;
using Atlas.Core.Tenancy;

namespace Atlas.Infrastructure.Services.Audit;

/// <summary>
/// 治理 M-G04-C2 默认实现：用 IResourceAccessGuard 的 view 动作做批量过滤。
///
/// 实现策略（最小可工作版）：
/// - 平台管理员直接全集返回；
/// - 其它用户对每个 (resourceType, resourceId) 调用一次 IResourceAccessGuard.CheckAsync(action="view")；
///   命中（任意 tier 允许）则保留；
/// - workspaceId 默认按 0 传入（service 层若能提供 workspaceId 应预先分组调用）。
///
/// 注：本实现复杂度 O(N)；后续可改为按 (workspace, role) 维度批量预解析降复杂度，
/// 先满足"语义正确 + 全局收口"。
/// </summary>
public sealed class ResourceVisibilityResolver : IResourceVisibilityResolver
{
    private readonly IResourceAccessGuard _guard;

    public ResourceVisibilityResolver(IResourceAccessGuard guard)
    {
        _guard = guard;
    }

    public async Task<IReadOnlyCollection<(string ResourceType, string ResourceId)>> FilterVisibleAsync(
        TenantId tenantId,
        long userId,
        bool isPlatformAdmin,
        IReadOnlyCollection<(string ResourceType, string ResourceId)> candidates,
        CancellationToken cancellationToken)
    {
        if (candidates.Count == 0)
        {
            return Array.Empty<(string, string)>();
        }
        if (isPlatformAdmin)
        {
            return candidates;
        }

        var visible = new List<(string ResourceType, string ResourceId)>(candidates.Count);
        foreach (var (resourceType, resourceId) in candidates)
        {
            if (string.IsNullOrWhiteSpace(resourceType) || string.IsNullOrWhiteSpace(resourceId))
            {
                continue;
            }
            if (!long.TryParse(resourceId, out var resourceIdLong))
            {
                continue;
            }
            var query = new ResourceAccessQuery(tenantId, userId, false, WorkspaceId: 0, resourceType, resourceIdLong, "view");
            var decision = await _guard.CheckAsync(query, cancellationToken);
            if (decision.Allowed)
            {
                visible.Add((resourceType, resourceId));
            }
        }
        return visible;
    }
}
