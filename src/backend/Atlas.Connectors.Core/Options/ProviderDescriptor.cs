namespace Atlas.Connectors.Core.Options;

/// <summary>
/// 描述一个 provider 在 ConnectorRegistry 中的元数据，用于 UI 列表、文档展示与能力探测。
/// 不存任何运行时凭据。
/// </summary>
public sealed record ProviderDescriptor
{
    public required string ProviderType { get; init; }

    public required string DisplayName { get; init; }

    /// <summary>provider 是否需要可信域名校验（企微典型场景）。</summary>
    public bool RequiresTrustedDomain { get; init; }

    /// <summary>是否支持服务端定时同步通讯录（用于 Hangfire RecurringJob 注册）。</summary>
    public bool SupportsDirectorySync { get; init; } = true;

    /// <summary>是否支持外部审批中心提单与状态回调。</summary>
    public bool SupportsApproval { get; init; } = true;

    /// <summary>是否支持模板卡片消息更新（如企微 update_template_card / 飞书 patch card）。</summary>
    public bool SupportsCardUpdate { get; init; } = true;

    /// <summary>provider 入站 webhook 的统一 topic 列表（如 approval-status / contact-change）。</summary>
    public IReadOnlyList<string> WebhookTopics { get; init; } = Array.Empty<string>();
}
