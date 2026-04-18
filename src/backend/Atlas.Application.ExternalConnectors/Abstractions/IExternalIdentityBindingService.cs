using Atlas.Application.ExternalConnectors.Models;
using Atlas.Connectors.Core.Models;
using Atlas.Domain.ExternalConnectors.Enums;

namespace Atlas.Application.ExternalConnectors.Abstractions;

/// <summary>
/// 身份绑定服务：4 档策略 + 冲突处理 + 审计。
/// 由 OAuth 回调 / Workflow 节点 / Controller 共同消费；调用方传入已解析的 ExternalUserProfile。
/// </summary>
public interface IExternalIdentityBindingService
{
    /// <summary>
    /// 根据 provider 与 ExternalUserProfile 命中绑定（已绑定直接返回；未绑定按 4 档策略尝试自动绑定）。
    /// </summary>
    Task<BindingResolutionResult> ResolveOrAttemptBindAsync(long providerId, ExternalUserProfile profile, IdentityBindingMatchStrategy strategy, CancellationToken cancellationToken);

    Task<ExternalIdentityBindingResponse> CreateManualAsync(ManualBindingRequest request, CancellationToken cancellationToken);

    Task<ExternalIdentityBindingResponse> ResolveConflictAsync(BindingConflictResolutionRequest request, CancellationToken cancellationToken);

    Task RevokeAsync(long bindingId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ExternalIdentityBindingListItem>> ListByProviderAsync(long providerId, IdentityBindingStatus? status, int skip, int take, CancellationToken cancellationToken);

    Task<int> CountByProviderAsync(long providerId, IdentityBindingStatus? status, CancellationToken cancellationToken);

    /// <summary>
    /// OAuth 登录成功后调用，记录最近一次登录时间。
    /// </summary>
    Task TouchLoginAsync(long bindingId, CancellationToken cancellationToken);
}

/// <summary>
/// ResolveOrAttemptBind 的结果。包含命中的 binding 与命中类型，便于上层决定签发 JWT 还是返回待绑定 ticket。
/// </summary>
public sealed class BindingResolutionResult
{
    public required BindingResolutionKind Kind { get; init; }

    public ExternalIdentityBindingResponse? Binding { get; init; }

    /// <summary>当 Kind==PendingManual 时携带的临时 ticket，用于前端进入待绑定页。</summary>
    public string? PendingTicket { get; init; }

    /// <summary>命中冲突时携带的另一条 binding 摘要，便于前端提示。</summary>
    public ExternalIdentityBindingListItem? ConflictWith { get; init; }
}

public enum BindingResolutionKind
{
    /// <summary>命中已存在的活跃绑定。</summary>
    Existing = 1,
    /// <summary>策略命中并自动新建绑定（Direct / Mobile / Email）。</summary>
    AutoCreated = 2,
    /// <summary>策略命中但需待管理员/用户人工确认（NameDept）。</summary>
    PendingConfirm = 3,
    /// <summary>未命中任何本地用户：返回 PendingTicket，前端进入手动绑定页。</summary>
    PendingManual = 4,
    /// <summary>检测到冲突（如手机号已绑定到其他本地用户）。</summary>
    Conflict = 5,
}
