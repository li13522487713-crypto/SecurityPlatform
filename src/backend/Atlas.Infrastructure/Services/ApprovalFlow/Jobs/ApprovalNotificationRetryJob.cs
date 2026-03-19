using Atlas.Application.Approval.Abstractions;
using Atlas.Application.Approval.Repositories;
using Atlas.Domain.Approval.Entities;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services.ApprovalFlow.Jobs;

/// <summary>
/// 审批通知重试后台任务
///
/// 定期扫描 ApprovalNotificationRetry 表中 nextRetryAt <= now 且 status = Pending 的记录，
/// 调用对应渠道的 Sender 重试发送。成功则标记完成，失败则递增 retryCount 并按指数退避
/// 计算下次重试时间。超过 maxRetries 后标记为 Failed 不再重试。
///
/// 建议注册为 Hangfire Recurring Job，每 5 分钟执行一次。
/// </summary>
public sealed class ApprovalNotificationRetryJob
{
    private readonly IApprovalNotificationRetryRepository _retryRepository;
    private readonly IEnumerable<IApprovalNotificationSender> _senders;
    private readonly ILogger<ApprovalNotificationRetryJob>? _logger;

    /// <summary>每批次最多处理的重试记录数（避免单次执行过久）</summary>
    private const int BatchSize = 50;

    public ApprovalNotificationRetryJob(
        IApprovalNotificationRetryRepository retryRepository,
        IEnumerable<IApprovalNotificationSender> senders,
        ILogger<ApprovalNotificationRetryJob>? logger = null)
    {
        _retryRepository = retryRepository;
        _senders = senders;
        _logger = logger;
    }

    /// <summary>
    /// 执行重试扫描（由 Hangfire 等调度器定期调用）
    /// </summary>
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var pendingRetries = await _retryRepository.GetPendingRetriesAsync(BatchSize, cancellationToken);

        if (pendingRetries.Count == 0)
        {
            return;
        }

        _logger?.LogInformation("通知重试 Job 拾取 {Count} 条待重试记录", pendingRetries.Count);

        foreach (var retry in pendingRetries)
        {
            await ProcessRetryAsync(retry, cancellationToken);
        }
    }

    private async Task ProcessRetryAsync(
        ApprovalNotificationRetry retry,
        CancellationToken cancellationToken)
    {
        var sender = _senders.FirstOrDefault(s => s.SupportedChannel == retry.Channel);
        if (sender == null)
        {
            _logger?.LogWarning(
                "通知重试跳过：渠道 {Channel} 无可用 Sender，记录 {RetryId}",
                retry.Channel, retry.Id);
            retry.RecordFailure($"No sender available for channel {retry.Channel}");
            await _retryRepository.UpdateAsync(retry, cancellationToken);
            return;
        }

        try
        {
            await sender.SendAsync(
                retry.TenantId,
                retry.RecipientUserId,
                retry.Title,
                retry.Content,
                cancellationToken);

            // 发送成功
            retry.MarkCompleted();
            await _retryRepository.UpdateAsync(retry, cancellationToken);

            _logger?.LogInformation(
                "通知重试成功：渠道 {Channel}，收件人 {UserId}，记录 {RetryId}",
                retry.Channel, retry.RecipientUserId, retry.Id);
        }
        catch (Exception ex)
        {
            // 发送失败，记录错误并计算下次重试时间
            retry.RecordFailure(ex.Message);
            await _retryRepository.UpdateAsync(retry, cancellationToken);

            if (retry.Status == NotificationRetryStatus.Failed)
            {
                _logger?.LogError(
                    "通知重试最终失败（已达最大重试次数）：渠道 {Channel}，收件人 {UserId}，记录 {RetryId}，错误：{Error}",
                    retry.Channel, retry.RecipientUserId, retry.Id, ex.Message);
            }
            else
            {
                _logger?.LogWarning(
                    "通知重试失败（第 {RetryCount}/{MaxRetries} 次）：渠道 {Channel}，收件人 {UserId}，记录 {RetryId}，下次重试 {NextRetry}",
                    retry.RetryCount, retry.MaxRetries, retry.Channel, retry.RecipientUserId, retry.Id, retry.NextRetryAt);
            }
        }
    }
}
