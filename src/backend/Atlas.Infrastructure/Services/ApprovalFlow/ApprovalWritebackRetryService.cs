using Atlas.Application.Approval.Repositories;
using Atlas.Application.Audit.Abstractions;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Audit.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services.ApprovalFlow;

/// <summary>
/// 审批状态回写重试服务。
/// 回写失败时通过指数退避重试（1s / 5s / 30s / 5min / 30min），
/// 超过 5 次后标记为死信并持久化到 ApprovalWritebackFailure 表。
/// </summary>
public sealed class ApprovalWritebackRetryService
{
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(30)
    ];

    private readonly IBackgroundWorkQueue _workQueue;
    private readonly IIdGeneratorAccessor _idGenerator;
    private readonly ILogger<ApprovalWritebackRetryService>? _logger;

    public ApprovalWritebackRetryService(
        IBackgroundWorkQueue workQueue,
        IIdGeneratorAccessor idGenerator,
        ILogger<ApprovalWritebackRetryService>? logger = null)
    {
        _workQueue = workQueue;
        _idGenerator = idGenerator;
        _logger = logger;
    }

    /// <summary>
    /// 将失败的回写任务投递到重试队列。
    /// </summary>
    public void EnqueueRetry(
        TenantId tenantId,
        string businessKey,
        string targetStatus,
        string initialErrorMessage,
        DateTimeOffset firstFailedAt)
    {
        var retryAttempt = 0;

        ScheduleAttempt(tenantId, businessKey, targetStatus, initialErrorMessage, firstFailedAt, retryAttempt);
    }

    private void ScheduleAttempt(
        TenantId tenantId,
        string businessKey,
        string targetStatus,
        string lastErrorMessage,
        DateTimeOffset firstFailedAt,
        int attemptIndex)
    {
        var delay = attemptIndex < RetryDelays.Length ? RetryDelays[attemptIndex] : TimeSpan.Zero;

        _workQueue.Enqueue(async (sp, ct) =>
        {
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay, ct);

            var syncHandler = sp.GetRequiredService<ApprovalStatusSyncHandler>();
            var failureRepo = sp.GetRequiredService<IApprovalWritebackFailureRepository>();
            var auditWriter = sp.GetRequiredService<IAuditWriter>();
            var idGen = sp.GetRequiredService<IIdGeneratorAccessor>();

            try
            {
                await syncHandler.SyncStatusAsync(tenantId, businessKey, targetStatus, ct);
                _logger?.LogInformation(
                    "审批回写重试成功（第 {Attempt} 次）：BusinessKey={BusinessKey}",
                    attemptIndex + 1, businessKey);
            }
            catch (Exception ex)
            {
                var nextAttempt = attemptIndex + 1;
                var errorMsg = ex.Message;

                if (nextAttempt < RetryDelays.Length)
                {
                    _logger?.LogWarning(ex,
                        "审批回写重试失败（第 {Attempt} 次），将在 {Delay} 后再试：BusinessKey={BusinessKey}",
                        nextAttempt, RetryDelays[nextAttempt], businessKey);

                    ScheduleAttempt(tenantId, businessKey, targetStatus, errorMsg, firstFailedAt, nextAttempt);
                }
                else
                {
                    // 超过最大重试次数，写入死信表
                    _logger?.LogError(ex,
                        "审批回写死信：BusinessKey={BusinessKey}，已重试 {MaxRetry} 次",
                        businessKey, nextAttempt);

                    var failure = new ApprovalWritebackFailure(
                        tenantId,
                        idGen.NextId(),
                        businessKey,
                        targetStatus,
                        nextAttempt,
                        errorMsg,
                        firstFailedAt,
                        DateTimeOffset.UtcNow);

                    await failureRepo.InsertAsync(failure, ct);

                    // 审计告警
                    var auditRecord = new AuditRecord(
                        tenantId,
                        "system",
                        "Approval.Writeback.Dead",
                        "Error",
                        $"BusinessKey:{businessKey}",
                        null,
                        null);
                    await auditWriter.WriteAsync(auditRecord, ct);
                }
            }
        });
    }
}
