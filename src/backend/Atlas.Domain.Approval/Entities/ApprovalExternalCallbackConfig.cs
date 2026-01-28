using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Enums;

namespace Atlas.Domain.Approval.Entities;

/// <summary>
/// 外部回调配置（对应 AntFlow 的 OutSideBpmCallbackUrlConf）
/// </summary>
public sealed class ApprovalExternalCallbackConfig : TenantEntity
{
    public ApprovalExternalCallbackConfig()
        : base(TenantId.Empty)
    {
        FlowDefinitionId = 0;
        CallbackUrl = string.Empty;
        SecretKey = string.Empty;
    }

    public ApprovalExternalCallbackConfig(
        TenantId tenantId,
        long flowDefinitionId,
        CallbackEventType eventType,
        string callbackUrl,
        string secretKey,
        long id)
        : base(tenantId)
    {
        Id = id;
        FlowDefinitionId = flowDefinitionId;
        EventType = eventType;
        CallbackUrl = callbackUrl;
        SecretKey = secretKey;
        IsEnabled = true;
        MaxRetryCount = 3;
        RetryIntervalSeconds = 300; // 5分钟
        TimeoutSeconds = 30;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>流程定义 ID（0 表示系统级配置）</summary>
    public long FlowDefinitionId { get; private set; }

    /// <summary>回调事件类型</summary>
    public CallbackEventType EventType { get; private set; }

    /// <summary>回调 URL</summary>
    public string CallbackUrl { get; private set; }

    /// <summary>签名密钥（用于安全校验）</summary>
    public string SecretKey { get; private set; }

    /// <summary>是否启用</summary>
    public bool IsEnabled { get; private set; }

    /// <summary>最大重试次数</summary>
    public int MaxRetryCount { get; private set; }

    /// <summary>重试间隔（秒）</summary>
    public int RetryIntervalSeconds { get; private set; }

    /// <summary>超时时间（秒）</summary>
    public int TimeoutSeconds { get; private set; }

    /// <summary>创建时间</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>更新时间</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Update(
        string callbackUrl,
        string secretKey,
        int maxRetryCount,
        int retryIntervalSeconds,
        int timeoutSeconds,
        DateTimeOffset now)
    {
        CallbackUrl = callbackUrl;
        SecretKey = secretKey;
        MaxRetryCount = maxRetryCount;
        RetryIntervalSeconds = retryIntervalSeconds;
        TimeoutSeconds = timeoutSeconds;
        UpdatedAt = now;
    }

    public void Enable()
    {
        IsEnabled = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Disable()
    {
        IsEnabled = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
