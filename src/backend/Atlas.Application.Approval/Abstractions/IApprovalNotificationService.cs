using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Approval.Enums;

namespace Atlas.Application.Approval.Abstractions;

/// <summary>
/// 审批消息通知服务接口
/// </summary>
public interface IApprovalNotificationService
{
    /// <summary>
    /// 发送通知
    /// </summary>
    /// <param name="tenantId">租户ID</param>
    /// <param name="eventType">事件类型</param>
    /// <param name="instance">流程实例</param>
    /// <param name="task">任务（可选）</param>
    /// <param name="recipientUserIds">收件人用户ID列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task NotifyAsync(
        TenantId tenantId,
        ApprovalNotificationEventType eventType,
        ApprovalProcessInstance instance,
        ApprovalTask? task,
        IReadOnlyList<long> recipientUserIds,
        CancellationToken cancellationToken);
}
