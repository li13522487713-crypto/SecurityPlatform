using System.Threading;
using System.Threading.Tasks;
using Atlas.Application.Authorization;
using Atlas.Application.Identity.Abstractions;
using Atlas.Core.Identity;
using Atlas.Core.Tenancy;

namespace Atlas.Infrastructure.Services.Authorization;

public sealed class ResourceWriteGate : IResourceWriteGate
{
    private readonly IResourceAccessGuard _guard;
    private readonly IPermissionDecisionService _pdp;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IResourceWorkspaceLookup _workspaceLookup;

    public ResourceWriteGate(
        IResourceAccessGuard guard,
        IPermissionDecisionService pdp,
        ICurrentUserAccessor currentUserAccessor,
        IResourceWorkspaceLookup workspaceLookup)
    {
        _guard = guard;
        _pdp = pdp;
        _currentUserAccessor = currentUserAccessor;
        _workspaceLookup = workspaceLookup;
    }

    public Task GuardAsync(
        TenantId tenantId,
        long workspaceId,
        string resourceType,
        long? resourceId,
        string action,
        CancellationToken cancellationToken)
    {
        var current = _currentUserAccessor.GetCurrentUser();
        var userId = current?.UserId ?? 0L;
        // 没有 user 上下文（系统/内部调度）按 platform admin 短路，保留既有行为；
        // 真实 HTTP 请求必有 ICurrentUserAccessor 填充，强制走三级合并判定。
        var isPlatformAdmin = current is null || current.IsPlatformAdmin;
        var query = new ResourceAccessQuery(tenantId, userId, isPlatformAdmin, workspaceId, resourceType, resourceId, action);
        return _guard.RequireAsync(query, cancellationToken);
    }

    public async Task GuardByResourceAsync(
        TenantId tenantId,
        string resourceType,
        long resourceId,
        string action,
        CancellationToken cancellationToken)
    {
        var workspaceId = await _workspaceLookup.ResolveWorkspaceIdAsync(tenantId, resourceType, resourceId, cancellationToken);
        if (workspaceId is not > 0)
        {
            // 资源不存在或老数据无 workspaceId → fallback 放行，保持向后兼容；让后续 service 自身抛 NotFound
            return;
        }
        await GuardAsync(tenantId, workspaceId.Value, resourceType, resourceId, action, cancellationToken);
    }

    public Task InvalidateAsync(
        TenantId tenantId,
        string resourceType,
        long resourceId,
        CancellationToken cancellationToken)
    {
        return _pdp.InvalidateResourceAsync(tenantId, resourceType, resourceId, cancellationToken);
    }
}
