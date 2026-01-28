using Atlas.Application.Approval.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Enums;

namespace Atlas.Infrastructure.Services.ApprovalFlow.NotificationSenders;

/// <summary>
/// App推送通知发送适配器（占位实现）
/// </summary>
public sealed class AppPushNotificationSender : IApprovalNotificationSender
{
    public ApprovalNotificationChannel SupportedChannel => ApprovalNotificationChannel.AppPush;

    public Task<bool> SendAsync(
        TenantId tenantId,
        long recipientUserId,
        string title,
        string content,
        CancellationToken cancellationToken)
    {
        // TODO: 实现App推送逻辑
        // 1. 根据 recipientUserId 查询用户设备Token
        // 2. 调用推送服务发送
        // 3. 记录发送日志
        return Task.FromResult(true);
    }
}
