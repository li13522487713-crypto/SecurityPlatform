using Atlas.Core.Tenancy;

namespace Atlas.Application.Approval.Abstractions;

/// <summary>
/// 催办服务接口
/// </summary>
public interface IApprovalReminderService
{
    /// <summary>
    /// 发送催办
    /// </summary>
    /// <param name="tenantId">租户ID</param>
    /// <param name="instanceId">流程实例ID</param>
    /// <param name="taskId">任务ID（可选）</param>
    /// <param name="reminderUserId">催办人用户ID</param>
    /// <param name="recipientUserId">被催办人用户ID</param>
    /// <param name="reminderMessage">催办消息</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task SendReminderAsync(
        TenantId tenantId,
        long instanceId,
        long? taskId,
        long reminderUserId,
        long recipientUserId,
        string reminderMessage,
        CancellationToken cancellationToken);
}
