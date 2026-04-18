using System.Collections.Concurrent;
using Atlas.Application.Audit.Abstractions;
using Atlas.Application.LowCode.Abstractions;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Audit.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Infrastructure.Services.LowCode.AgentChannels;

/// <summary>
/// 渠道适配器基类（P3-1 PLAN §M18 S18-2）：4 个渠道的共享发布/接收骨架。
///
/// 当前 4 个具体实现（Feishu / WeChat / Douyin / Doubao）均按"未配置凭据 → not_configured，
/// 配置后由生产部署阶段通过 services.Replace 注入真实 OAuth/Webhook 实现"的方式落地，
/// 与 FINAL 报告"模型/外部依赖延后项"一致；同时让 Studio "多渠道发布" UI 4 渠道齐全可见。
/// </summary>
internal abstract class AgentChannelAdapterBase : IAgentChannelAdapter
{
    private readonly IAgentRuntimeRegistry _registry;
    private readonly IAuditWriter _auditWriter;

    protected AgentChannelAdapterBase(IAgentRuntimeRegistry registry, IAuditWriter auditWriter)
    {
        _registry = registry;
        _auditWriter = auditWriter;
    }

    public abstract string Channel { get; }
    public abstract string DisplayName { get; }

    /// <summary>子类校验自身凭据是否就绪；默认以 ChannelConfigJson 是否非空为准。</summary>
    protected virtual bool IsCredentialConfigured(string? configJson) => !string.IsNullOrWhiteSpace(configJson);

    public Task<AgentChannelStatus> GetStatusAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        // P3-1 stub：读不到租户级配置时返 not_configured；接入真实凭据存储后改为查询配置中心。
        return Task.FromResult(new AgentChannelStatus(
            Channel,
            "not_configured",
            $"{DisplayName} 渠道尚未为租户 {tenantId.Value} 配置凭据；请在租户管理后台设置 OAuth / Webhook 参数。"));
    }

    public async Task<AgentChannelPublishResult> PublishAsync(
        TenantId tenantId,
        long currentUserId,
        AgentChannelPublishRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsCredentialConfigured(request.ChannelConfigJson))
        {
            await _auditWriter.WriteAsync(new AuditRecord(
                tenantId, currentUserId.ToString(),
                $"lowcode.agent.channel.{Channel}.publish", "failed",
                $"agent:{request.AgentId}:reason:credentials-missing", null, null), cancellationToken);
            return new AgentChannelPublishResult(false, null, null, "AGENT_CHANNEL_NOT_CONFIGURED",
                $"{DisplayName} 渠道凭据未配置；请在 ChannelConfigJson 中提供 OAuth/Webhook 参数。");
        }

        var descriptor = new AgentRuntimeEntityDescriptor(
            RuntimeEntityId: $"art_{Channel}_{Guid.NewGuid():N}",
            Channel: Channel,
            AgentId: request.AgentId,
            ModelId: null,
            PromptTemplateId: null,
            ConfigJson: request.ChannelConfigJson,
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow);

        var entityId = await _registry.RegisterAsync(tenantId, currentUserId, descriptor, cancellationToken);
        await _auditWriter.WriteAsync(new AuditRecord(
            tenantId, currentUserId.ToString(),
            $"lowcode.agent.channel.{Channel}.publish", "success",
            $"agent:{request.AgentId}:entity:{entityId}", null, null), cancellationToken);
        return new AgentChannelPublishResult(true, entityId, BuildPublicEndpoint(entityId), null, null);
    }

    public Task<AgentChannelReceiveResult> ReceiveAsync(
        TenantId tenantId,
        AgentChannelReceiveRequest request,
        CancellationToken cancellationToken)
    {
        // P3-1 stub：真实验签 / 消息解码 / 事件回调由具体渠道实现。
        return Task.FromResult(new AgentChannelReceiveResult(
            false,
            null,
            "AGENT_CHANNEL_RECEIVER_NOT_CONFIGURED",
            $"{DisplayName} 渠道接收器尚未接入真实 SDK；当前仅返回协议占位响应。"));
    }

    protected virtual string BuildPublicEndpoint(string runtimeEntityId)
        => $"https://channels.atlas.local/{Channel}/{runtimeEntityId}";
}

internal sealed class FeishuChannelAdapter : AgentChannelAdapterBase
{
    public FeishuChannelAdapter(IAgentRuntimeRegistry registry, IAuditWriter auditWriter) : base(registry, auditWriter) { }
    public override string Channel => "feishu";
    public override string DisplayName => "飞书";
}

internal sealed class WeChatChannelAdapter : AgentChannelAdapterBase
{
    public WeChatChannelAdapter(IAgentRuntimeRegistry registry, IAuditWriter auditWriter) : base(registry, auditWriter) { }
    public override string Channel => "wechat";
    public override string DisplayName => "微信";
}

internal sealed class DouyinChannelAdapter : AgentChannelAdapterBase
{
    public DouyinChannelAdapter(IAgentRuntimeRegistry registry, IAuditWriter auditWriter) : base(registry, auditWriter) { }
    public override string Channel => "douyin";
    public override string DisplayName => "抖音";
}

internal sealed class DoubaoChannelAdapter : AgentChannelAdapterBase
{
    public DoubaoChannelAdapter(IAgentRuntimeRegistry registry, IAuditWriter auditWriter) : base(registry, auditWriter) { }
    public override string Channel => "doubao";
    public override string DisplayName => "豆包";
}

/// <summary>
/// 渠道运行实体注册中心：进程内字典 + 周期持久化（当前以内存字典骨架，
/// 与 NodeStateStore 类似在生产部署用 SqlSugar 实体落库）。
/// 4 类查询：按 entityId 拿 / 按渠道列出 / 注销 / 注册。
/// </summary>
internal sealed class InMemoryAgentRuntimeRegistry : IAgentRuntimeRegistry
{
    private readonly ConcurrentDictionary<string, (TenantId Tenant, AgentRuntimeEntityDescriptor Descriptor)> _store = new();
    // 注意：IAuditWriter 是 Scoped，本类是 Singleton，因此通过 IServiceScopeFactory 按需创建 scope 解析，
    // 避免触发 DI ValidateScopes 报错，同时保持进程内字典 _store 全局共享。
    private readonly IServiceScopeFactory _scopeFactory;

    public InMemoryAgentRuntimeRegistry(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    private async Task WriteAuditAsync(AuditRecord record, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var auditWriter = scope.ServiceProvider.GetRequiredService<IAuditWriter>();
        await auditWriter.WriteAsync(record, cancellationToken);
    }

    public async Task<string> RegisterAsync(TenantId tenantId, long currentUserId, AgentRuntimeEntityDescriptor descriptor, CancellationToken cancellationToken)
    {
        _store[descriptor.RuntimeEntityId] = (tenantId, descriptor);
        await WriteAuditAsync(new AuditRecord(
            tenantId, currentUserId.ToString(),
            "lowcode.agent.runtime-entity.register", "success",
            $"channel:{descriptor.Channel}:agent:{descriptor.AgentId}:entity:{descriptor.RuntimeEntityId}", null, null), cancellationToken);
        return descriptor.RuntimeEntityId;
    }

    public Task<AgentRuntimeEntityDescriptor?> GetAsync(TenantId tenantId, string runtimeEntityId, CancellationToken cancellationToken)
    {
        if (_store.TryGetValue(runtimeEntityId, out var v) && v.Tenant.Value == tenantId.Value)
            return Task.FromResult<AgentRuntimeEntityDescriptor?>(v.Descriptor);
        return Task.FromResult<AgentRuntimeEntityDescriptor?>(null);
    }

    public Task<IReadOnlyList<AgentRuntimeEntityDescriptor>> ListByChannelAsync(TenantId tenantId, string channel, CancellationToken cancellationToken)
    {
        var list = _store.Values
            .Where(x => x.Tenant.Value == tenantId.Value && string.Equals(x.Descriptor.Channel, channel, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Descriptor)
            .ToList();
        return Task.FromResult<IReadOnlyList<AgentRuntimeEntityDescriptor>>(list);
    }

    public async Task UnregisterAsync(TenantId tenantId, long currentUserId, string runtimeEntityId, CancellationToken cancellationToken)
    {
        if (_store.TryRemove(runtimeEntityId, out _))
        {
            await WriteAuditAsync(new AuditRecord(
                tenantId, currentUserId.ToString(),
                "lowcode.agent.runtime-entity.unregister", "success",
                $"entity:{runtimeEntityId}", null, null), cancellationToken);
        }
    }
}
