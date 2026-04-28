using Atlas.Application.ExternalConnectors.Models;
using Atlas.Connectors.Core.Models;

namespace Atlas.Application.ExternalConnectors.Abstractions;

/// <summary>
/// 把本地审批事件 fan-out 到外部 provider 的统一入口。
/// 由现有 Approval 体系（ExternalApprovalFanoutHandler）调用，再由 IExternalApprovalProvider 真正执行外部 API。
/// </summary>
public interface IExternalApprovalDispatchService
{
    /// <summary>
    /// 当本地审批实例创建完成后调用：根据 (ApprovalFlowDefinitionId) 找到映射，按 IntegrationMode 决定是否推外部审批 + 写 link。
    /// </summary>
    Task<ExternalApprovalDispatchResult> OnInstanceStartedAsync(long localInstanceId, long flowDefinitionId, ExternalApprovalSubmission payload, CancellationToken cancellationToken);

    /// <summary>
    /// 当本地审批状态变更（已通过 / 已拒绝 / 已撤回）时调用：更新外部实例 + 卡片。
    /// </summary>
    Task OnInstanceStatusChangedAsync(long localInstanceId, ExternalApprovalStatus newStatus, string? commentText, CancellationToken cancellationToken);
}

public sealed class ExternalApprovalDispatchResult
{
    public bool Pushed { get; set; }

    public string? ExternalInstanceId { get; set; }

    public string? ProviderType { get; set; }

    public long? ProviderId { get; set; }

    public string? Reason { get; set; }
}
