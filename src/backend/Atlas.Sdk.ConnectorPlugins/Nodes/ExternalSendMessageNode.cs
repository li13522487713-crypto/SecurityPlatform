using Atlas.Connectors.Core;
using Atlas.Connectors.Core.Abstractions;
using Atlas.Connectors.Core.Models;
using Atlas.Application.ExternalConnectors.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.ExternalConnectors.Enums;

namespace Atlas.Sdk.ConnectorPlugins.Nodes;

/// <summary>
/// "发送外部消息" 节点（统一支持企微 / 飞书）。
/// inputs:
///   - providerId: long
///   - userIds: string[]（外部 user id 列表）
///   - messageType: "text" | "card"
///   - text / title / content / jumpUrl
/// outputs:
///   - messageId: string
/// </summary>
public abstract class ExternalSendMessageNodeBase : IConnectorPluginNode
{
    private readonly IConnectorRegistry _registry;
    private readonly IExternalIdentityProviderRepository _providerRepository;
    private readonly ITenantProvider _tenantProvider;

    protected ExternalSendMessageNodeBase(IConnectorRegistry registry, IExternalIdentityProviderRepository providerRepository, ITenantProvider tenantProvider)
    {
        _registry = registry;
        _providerRepository = providerRepository;
        _tenantProvider = tenantProvider;
    }

    protected abstract ConnectorProviderType TargetProviderType { get; }

    public abstract string NodeType { get; }
    public abstract string DisplayName { get; }
    public string Category => "external_collaboration";

    public async Task<ConnectorPluginNodeResult> ExecuteAsync(ConnectorPluginNodeContext context, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _tenantProvider.GetTenantId();
            var providerId = Convert.ToInt64(context.Inputs.GetValueOrDefault("providerId") ?? context.ProviderInstanceId);
            var provider = await _providerRepository.GetByIdAsync(tenantId, providerId, cancellationToken).ConfigureAwait(false);
            if (provider is null || provider.ProviderType != TargetProviderType || !provider.Enabled)
            {
                return new ConnectorPluginNodeResult { Success = false, ErrorCode = "PROVIDER_NOT_AVAILABLE" };
            }
            var providerType = provider.ProviderType.ToProviderType();
            var messaging = _registry.GetMessaging(providerType);
            var ctx = new ConnectorContext { TenantId = tenantId.Value, ProviderInstanceId = provider.Id, ProviderType = providerType };

            var userIds = context.Inputs.GetValueOrDefault("userIds") as IEnumerable<object> ?? Array.Empty<object>();
            var recipient = new ExternalMessageRecipient { UserIds = userIds.Select(u => u?.ToString() ?? string.Empty).Where(s => !string.IsNullOrEmpty(s)).ToArray() };

            var messageType = context.Inputs.GetValueOrDefault("messageType")?.ToString() ?? "text";
            ExternalMessageDispatchResult result;
            if (string.Equals(messageType, "card", StringComparison.OrdinalIgnoreCase))
            {
                var card = new ExternalMessageCard
                {
                    Title = context.Inputs.GetValueOrDefault("title")?.ToString() ?? "通知",
                    Subtitle = context.Inputs.GetValueOrDefault("subtitle")?.ToString(),
                    Content = context.Inputs.GetValueOrDefault("content")?.ToString(),
                    JumpUrl = context.Inputs.GetValueOrDefault("jumpUrl")?.ToString(),
                };
                result = await messaging.SendCardAsync(ctx, recipient, card, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                var text = context.Inputs.GetValueOrDefault("text")?.ToString() ?? string.Empty;
                result = await messaging.SendTextAsync(ctx, recipient, text, cancellationToken).ConfigureAwait(false);
            }
            return new ConnectorPluginNodeResult
            {
                Success = true,
                Outputs = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["messageId"] = result.MessageId,
                    ["responseCode"] = result.ResponseCode,
                },
            };
        }
        catch (Exception ex)
        {
            return new ConnectorPluginNodeResult { Success = false, ErrorCode = ex.GetType().Name, ErrorMessage = ex.Message };
        }
    }
}

public sealed class WeComSendMessageNode : ExternalSendMessageNodeBase
{
    public WeComSendMessageNode(IConnectorRegistry registry, IExternalIdentityProviderRepository providerRepository, ITenantProvider tenantProvider)
        : base(registry, providerRepository, tenantProvider) { }
    protected override ConnectorProviderType TargetProviderType => ConnectorProviderType.WeCom;
    public override string NodeType => "wecom_send_message";
    public override string DisplayName => "发送企微消息";
}

public sealed class FeishuSendMessageNode : ExternalSendMessageNodeBase
{
    public FeishuSendMessageNode(IConnectorRegistry registry, IExternalIdentityProviderRepository providerRepository, ITenantProvider tenantProvider)
        : base(registry, providerRepository, tenantProvider) { }
    protected override ConnectorProviderType TargetProviderType => ConnectorProviderType.Feishu;
    public override string NodeType => "feishu_send_message";
    public override string DisplayName => "发送飞书消息";
}

public sealed class DingTalkSendMessageNode : ExternalSendMessageNodeBase
{
    public DingTalkSendMessageNode(IConnectorRegistry registry, IExternalIdentityProviderRepository providerRepository, ITenantProvider tenantProvider)
        : base(registry, providerRepository, tenantProvider) { }
    protected override ConnectorProviderType TargetProviderType => ConnectorProviderType.DingTalk;
    public override string NodeType => "dingtalk_send_message";
    public override string DisplayName => "发送钉钉工作通知";
}
