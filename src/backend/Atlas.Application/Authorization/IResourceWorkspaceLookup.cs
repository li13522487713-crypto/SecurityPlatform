using System.Threading;
using System.Threading.Tasks;
using Atlas.Core.Tenancy;

namespace Atlas.Application.Authorization;

/// <summary>
/// 治理 R1-B4：按 (resourceType, resourceId) 解析所属 WorkspaceId，给 IResourceWriteGate 自动派单 ACL。
///
/// 支持的 resourceType（与 IResourceCollaboratorService.AllowedResourceTypes 对齐）：
/// agent / workflow / app / knowledge / database / plugin。
/// 找不到资源或 WorkspaceId 为空时返回 null（控制器 fallback 跳过守卫，保持向后兼容）。
/// </summary>
public interface IResourceWorkspaceLookup
{
    Task<long?> ResolveWorkspaceIdAsync(
        TenantId tenantId,
        string resourceType,
        long resourceId,
        CancellationToken cancellationToken);
}
