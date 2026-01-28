using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Enums;

namespace Atlas.Application.Approval.Abstractions;

/// <summary>
/// 审批消息发送适配器接口（Email/SMS/AppPush）
/// </summary>
public interface IApprovalNotificationSender
{
    /// <summary>支持的通知渠道</summary>
    ApprovalNotificationChannel SupportedChannel { get; }

    /// <summary>发送消息</summary>
    /// <param name="tenantId">租户ID</param>
    /// <param name="recipientUserId">收件人用户ID</param>
    /// <param name="title">消息标题</param>
    /// <param name="content">消息内容</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否发送成功</returns>
    Task<bool> SendAsync(
        TenantId tenantId,
        long recipientUserId,
        string title,
        string content,
        CancellationToken cancellationToken);
}
