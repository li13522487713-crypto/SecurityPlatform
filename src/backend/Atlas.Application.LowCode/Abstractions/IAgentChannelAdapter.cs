using Atlas.Core.Tenancy;

namespace Atlas.Application.LowCode.Abstractions;

/// <summary>
/// 智能体渠道适配器接口（P3-1 PLAN §M18 S18-2 + S18-6）。
///
/// 4 渠道：飞书 / 微信 / 抖音 / 豆包。每个渠道实现：
///  - OAuth 配置 + Webhook 接收 + 消息推送回调 + 消息格式映射
///  - 注入到 <see cref="IAgentRuntimeRegistry"/> 由 channel 名路由。
///
/// 当前提供 4 个 Stub 实现（凭据未配置时返回 NotConfigured 状态），
/// 让 Studio "多渠道发布" UI 可以展示完整 4 渠道列表，并在用户配置真实凭据后无缝切换。
/// </summary>
public interface IAgentChannelAdapter
{
    /// <summary>渠道唯一标识（feishu / wechat / douyin / doubao）。</summary>
    string Channel { get; }

    /// <summary>渠道展示名（中文）。</summary>
    string DisplayName { get; }

    /// <summary>
    /// 校验当前租户是否已为本渠道配置必要凭据；未配置时 Status="not_configured"，
    /// 配置后 Status="ready"。
    /// </summary>
    Task<AgentChannelStatus> GetStatusAsync(TenantId tenantId, CancellationToken cancellationToken);

    /// <summary>
    /// 将一个智能体发布到本渠道：返回渠道运行实体 id（runtimeEntityId），
    /// 该 entity id 由 <see cref="IAgentRuntimeRegistry"/> 按 channel 路由表持久化。
    /// </summary>
    Task<AgentChannelPublishResult> PublishAsync(
        TenantId tenantId,
        long currentUserId,
        AgentChannelPublishRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// 接收 Webhook 回调：把渠道推送（用户消息 / 事件）转为内部统一消息格式。
    /// 凭据未配置 → 返回 ReceiveResult.Error。
    /// </summary>
    Task<AgentChannelReceiveResult> ReceiveAsync(
        TenantId tenantId,
        AgentChannelReceiveRequest request,
        CancellationToken cancellationToken);
}

public sealed record AgentChannelStatus(
    string Channel,
    /// <summary>"not_configured" | "ready" | "error"。</summary>
    string Status,
    string? Detail);

public sealed record AgentChannelPublishRequest(
    string AgentId,
    /// <summary>发布版本（与 Agent 版本归档对齐）。</summary>
    string? Version,
    /// <summary>渠道特化配置 JSON（如飞书 app_id / app_secret，微信 token / encoding_aes_key 等）。</summary>
    string? ChannelConfigJson);

public sealed record AgentChannelPublishResult(
    bool Success,
    /// <summary>渠道运行实体 id（即"渠道路由表"主键）。</summary>
    string? RuntimeEntityId,
    string? PublicEndpoint,
    string? ErrorCode,
    string? ErrorMessage);

public sealed record AgentChannelReceiveRequest(
    string RuntimeEntityId,
    /// <summary>原始 webhook payload JSON。</summary>
    string PayloadJson,
    /// <summary>渠道签名 / 验签头（如飞书 X-Lark-Request-Timestamp + X-Lark-Signature）。</summary>
    IReadOnlyDictionary<string, string>? Headers);

public sealed record AgentChannelReceiveResult(
    bool Success,
    /// <summary>归一化的内部消息（channel="feishu" | "wechat" | ...）。</summary>
    NormalizedAgentMessage? Message,
    string? ErrorCode,
    string? ErrorMessage);

public sealed record NormalizedAgentMessage(
    string Channel,
    string ConversationId,
    string UserId,
    string MessageType,
    string Text,
    DateTimeOffset OccurredAt);

/// <summary>
/// 渠道运行实体注册中心（P3-1 PLAN §M18 S18-6）。
/// 渠道发布时 → 构建运行实体（含模型 + 技能 + 记忆 + 提示词 + 配额绑定）→ 注册 + 渠道路由表更新。
/// </summary>
public interface IAgentRuntimeRegistry
{
    Task<string> RegisterAsync(
        TenantId tenantId,
        long currentUserId,
        AgentRuntimeEntityDescriptor descriptor,
        CancellationToken cancellationToken);

    Task<AgentRuntimeEntityDescriptor?> GetAsync(
        TenantId tenantId,
        string runtimeEntityId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AgentRuntimeEntityDescriptor>> ListByChannelAsync(
        TenantId tenantId,
        string channel,
        CancellationToken cancellationToken);

    Task UnregisterAsync(
        TenantId tenantId,
        long currentUserId,
        string runtimeEntityId,
        CancellationToken cancellationToken);
}

public sealed record AgentRuntimeEntityDescriptor(
    string RuntimeEntityId,
    string Channel,
    string AgentId,
    string? ModelId,
    string? PromptTemplateId,
    /// <summary>额外配置 JSON（渠道凭据 / 路由 / 配额绑定等）。</summary>
    string? ConfigJson,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
