using Atlas.Application.LowCode.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.LowCode.Abstractions;

public interface IRuntimeTriggerService
{
    Task<IReadOnlyList<TriggerInfoDto>> ListAsync(TenantId tenantId, CancellationToken cancellationToken);
    Task<TriggerInfoDto> UpsertAsync(TenantId tenantId, long currentUserId, TriggerUpsertRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(TenantId tenantId, long currentUserId, string triggerId, CancellationToken cancellationToken);
    Task PauseAsync(TenantId tenantId, long currentUserId, string triggerId, CancellationToken cancellationToken);
    Task ResumeAsync(TenantId tenantId, long currentUserId, string triggerId, CancellationToken cancellationToken);
    /// <summary>由 Hangfire CRON 调度器调用：执行触发动作。</summary>
    Task FireAsync(TenantId tenantId, string triggerId, CancellationToken cancellationToken);
    /// <summary>
    /// 重置/生成 webhook 共享密钥（仅 kind=webhook 触发器有效，返回新的明文密钥；只在创建时返回一次）。
    /// </summary>
    Task<string> RotateWebhookSecretAsync(TenantId tenantId, long currentUserId, string triggerId, CancellationToken cancellationToken);

    /// <summary>
    /// Webhook 外部回调入口（无需登录态）：根据 triggerId + 请求头 X-Atlas-Webhook-Secret 校验并触发。
    /// 校验失败抛 BusinessException("WEBHOOK_INVALID_SECRET", ...)；触发器不存在或已禁用同样抛错。
    /// </summary>
    Task FireWebhookAsync(TenantId tenantId, string triggerId, string providedSecret, CancellationToken cancellationToken);

    /// <summary>
    /// 触发某事件名：扫描 kind=event AND EventName=eventName AND Enabled=true 的所有触发器，
    /// 全部 FireAsync。返回实际被触发的触发器数量。
    /// 由内部业务事件（如发布/审批/工单状态变更）发布；亦可作为 dispatch.run 的旁路入口。
    /// </summary>
    Task<int> RaiseEventAsync(TenantId tenantId, string eventName, CancellationToken cancellationToken);
}

public interface IRuntimeWebviewDomainService
{
    Task<IReadOnlyList<WebviewDomainInfoDto>> ListAsync(TenantId tenantId, CancellationToken cancellationToken);
    Task<WebviewDomainInfoDto> AddAsync(TenantId tenantId, long currentUserId, AddWebviewDomainRequest request, CancellationToken cancellationToken);
    Task<WebviewDomainInfoDto> VerifyAsync(TenantId tenantId, long currentUserId, long id, CancellationToken cancellationToken);
    Task RemoveAsync(TenantId tenantId, long currentUserId, long id, CancellationToken cancellationToken);
}
