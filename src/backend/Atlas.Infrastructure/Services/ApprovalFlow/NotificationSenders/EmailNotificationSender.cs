using Atlas.Application.Approval.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Enums;

namespace Atlas.Infrastructure.Services.ApprovalFlow.NotificationSenders;

/// <summary>
/// 邮件通知发送适配器（占位实现）
/// </summary>
public sealed class EmailNotificationSender : IApprovalNotificationSender
{
    public ApprovalNotificationChannel SupportedChannel => ApprovalNotificationChannel.Email;

    public Task<bool> SendAsync(
        TenantId tenantId,
        long recipientUserId,
        string title,
        string content,
        CancellationToken cancellationToken)
    {
        // TODO: 实现邮件发送逻辑
        // 1. 根据 recipientUserId 查询用户邮箱
        // 2. 调用邮件服务发送
        // 3. 记录发送日志
        return Task.FromResult(true);
    }
}
