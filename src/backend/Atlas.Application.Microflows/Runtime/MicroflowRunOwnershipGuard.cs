using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Exceptions;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Repositories;
using Atlas.Domain.Microflows.Entities;

namespace Atlas.Application.Microflows.Runtime;

/// <summary>
/// 校验调用方是否有权读取/取消某个 microflow run（按 workspaceId / tenantId 维度）。
/// 用于消除 P0-3 IDOR：仅凭 runId 即可读取他人 trace/session/cancel。
///
/// 校验顺序：
///   1. session.WorkspaceId/TenantId 优先；
///   2. 旧数据 session.WorkspaceId 为 NULL 时回退到 resource.WorkspaceId/TenantId；
///   3. 当请求上下文 workspaceId 缺失时拒绝。
/// </summary>
public interface IMicroflowRunOwnershipGuard
{
    /// <summary>
    /// 加载 session 并校验归属。归属不通过抛 <see cref="MicroflowApiException"/>(404) 以避免存在性泄漏。
    /// </summary>
    Task<MicroflowRunSessionEntity> EnsureRunOwnedAsync(string runId, CancellationToken cancellationToken);
}

public sealed class MicroflowRunOwnershipGuard : IMicroflowRunOwnershipGuard
{
    private readonly IMicroflowRunRepository _runRepository;
    private readonly IMicroflowResourceRepository _resourceRepository;
    private readonly IMicroflowRequestContextAccessor _requestContextAccessor;

    public MicroflowRunOwnershipGuard(
        IMicroflowRunRepository runRepository,
        IMicroflowResourceRepository resourceRepository,
        IMicroflowRequestContextAccessor requestContextAccessor)
    {
        _runRepository = runRepository;
        _resourceRepository = resourceRepository;
        _requestContextAccessor = requestContextAccessor;
    }

    public async Task<MicroflowRunSessionEntity> EnsureRunOwnedAsync(string runId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(runId))
        {
            throw new MicroflowApiException(MicroflowApiErrorCode.MicroflowNotFound, "微流运行会话不存在。", 404);
        }

        var session = await _runRepository.GetSessionAsync(runId, cancellationToken)
            ?? throw new MicroflowApiException(MicroflowApiErrorCode.MicroflowNotFound, "微流运行会话不存在。", 404);

        var ctx = _requestContextAccessor.Current;
        var ctxWorkspace = ctx.WorkspaceId;
        var ctxTenant = ctx.TenantId;

        // 当上下文缺少 workspace 时直接拒绝，避免无 workspace header 的越权读取。
        if (string.IsNullOrWhiteSpace(ctxWorkspace))
        {
            throw new MicroflowApiException(MicroflowApiErrorCode.MicroflowNotFound, "微流运行会话不存在。", 404);
        }

        var sessionWorkspace = session.WorkspaceId;
        var sessionTenant = session.TenantId;

        // 旧数据 session 没填 workspace；用 resource 反查（resource 已保留 workspace 列）。
        if (string.IsNullOrWhiteSpace(sessionWorkspace) || string.IsNullOrWhiteSpace(sessionTenant))
        {
            var resource = await _resourceRepository.GetByIdAsync(session.ResourceId, cancellationToken);
            sessionWorkspace ??= resource?.WorkspaceId;
            sessionTenant ??= resource?.TenantId;
        }

        if (!string.Equals(sessionWorkspace, ctxWorkspace, StringComparison.Ordinal))
        {
            // 不暴露存在性
            throw new MicroflowApiException(MicroflowApiErrorCode.MicroflowNotFound, "微流运行会话不存在。", 404);
        }

        if (!string.IsNullOrWhiteSpace(ctxTenant)
            && !string.IsNullOrWhiteSpace(sessionTenant)
            && !string.Equals(sessionTenant, ctxTenant, StringComparison.Ordinal))
        {
            throw new MicroflowApiException(MicroflowApiErrorCode.MicroflowNotFound, "微流运行会话不存在。", 404);
        }

        return session;
    }
}
