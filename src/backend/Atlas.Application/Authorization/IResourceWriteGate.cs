using System.Threading;
using System.Threading.Tasks;
using Atlas.Core.Tenancy;

namespace Atlas.Application.Authorization;

/// <summary>
/// 治理 M-G03-C3..C5（S6）：资源写动作守卫的「面向调用者」便捷入口。
///
/// 内部组合 <see cref="IResourceAccessGuard.RequireAsync"/> + <see cref="Atlas.Application.Identity.Abstractions.IPermissionDecisionService.InvalidateResourceAsync"/>，
/// 让 Controller / Service 用一行代码完成「写前鉴权 + 写后失效」的标准模板。
///
/// 设计：
/// - 当前请求若没有 user 上下文（系统调度、内部 dispatch），<see cref="GuardAsync"/> 默认按 isPlatformAdmin=true 处理，
///   保证既有内部调用不被破坏；S5 已证明 IsPlatformAdmin 短路。
/// - <see cref="InvalidateAsync"/> 任何时候都会调用 PDP 失效，确保 ACL 缓存最终一致。
/// </summary>
public interface IResourceWriteGate
{
    /// <summary>写动作前置检查；失败抛 BusinessException(Forbidden)。</summary>
    Task GuardAsync(
        TenantId tenantId,
        long workspaceId,
        string resourceType,
        long? resourceId,
        string action,
        CancellationToken cancellationToken);

    /// <summary>写动作成功后失效该资源 PDP 缓存（按资源 tag）。</summary>
    Task InvalidateAsync(
        TenantId tenantId,
        string resourceType,
        long resourceId,
        CancellationToken cancellationToken);
}
