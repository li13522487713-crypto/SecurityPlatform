using Atlas.Application.Approval.Abstractions;
using Atlas.Application.ExternalConnectors.Repositories;
using Atlas.Connectors.Core;
using Atlas.Connectors.Core.Abstractions;
using Atlas.Connectors.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Enums;
using Atlas.Domain.ExternalConnectors.Enums;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.ExternalConnectors.Services;

/// <summary>
/// 把审批通知通过企业微信 / 飞书消息卡片发出去。
/// 通过 ExternalIdentityBinding 把本地 RecipientUserId 解析为外部 user id；
/// 没有绑定时直接返回 false，让上层走重试或回退到其他渠道。
/// </summary>
public abstract class ExternalMessagingApprovalNotificationSenderBase : IApprovalNotificationSender
{
    private readonly IConnectorRegistry _registry;
    private readonly IExternalIdentityProviderRepository _providerRepository;
    private readonly IExternalIdentityBindingRepository _bindingRepository;
    private readonly ILogger _logger;

    protected ExternalMessagingApprovalNotificationSenderBase(
        IConnectorRegistry registry,
        IExternalIdentityProviderRepository providerRepository,
        IExternalIdentityBindingRepository bindingRepository,
        ILogger logger)
    {
        _registry = registry;
        _providerRepository = providerRepository;
        _bindingRepository = bindingRepository;
        _logger = logger;
    }

    public abstract ApprovalNotificationChannel SupportedChannel { get; }

    protected abstract ConnectorProviderType TargetProviderType { get; }

    public async Task<bool> SendAsync(TenantId tenantId, long recipientUserId, string title, string content, CancellationToken cancellationToken)
    {
        // 找到该租户下"该 provider 类型且启用"的第一个 provider 实例
        var providers = await _providerRepository.ListAsync(tenantId, TargetProviderType, includeDisabled: false, cancellationToken).ConfigureAwait(false);
        var provider = providers.FirstOrDefault();
        if (provider is null)
        {
            _logger.LogDebug("No enabled {ProviderType} provider for tenant {TenantId}; skip {Channel} notification.", TargetProviderType, tenantId.Value, SupportedChannel);
            return false;
        }

        var bindings = await _bindingRepository.GetByLocalUserIdAsync(tenantId, recipientUserId, cancellationToken).ConfigureAwait(false);
        var binding = bindings.FirstOrDefault(b => b.ProviderId == provider.Id && b.Status == IdentityBindingStatus.Active);
        if (binding is null)
        {
            _logger.LogDebug("Local user {UserId} has no active {ProviderType} binding; skip {Channel} notification.", recipientUserId, TargetProviderType, SupportedChannel);
            return false;
        }

        var providerType = provider.ProviderType.ToProviderType();
        var messaging = _registry.GetMessaging(providerType);
        var ctx = new ConnectorContext { TenantId = tenantId.Value, ProviderInstanceId = provider.Id, ProviderType = providerType };
        var card = new ExternalMessageCard
        {
            Title = title,
            Subtitle = string.Empty,
            Content = content,
            Tone = "info",
            Actions = Array.Empty<ExternalMessageCardAction>(),
        };
        var recipient = new ExternalMessageRecipient { UserIds = new[] { binding.ExternalUserId } };

        try
        {
            await messaging.SendCardAsync(ctx, recipient, card, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (ConnectorException ex)
        {
            _logger.LogWarning(ex, "External {ProviderType} card send failed for binding {BindingId}.", providerType, binding.Id);
            return false;
        }
    }
}

public sealed class WeComApprovalNotificationSender : ExternalMessagingApprovalNotificationSenderBase
{
    public WeComApprovalNotificationSender(
        IConnectorRegistry registry,
        IExternalIdentityProviderRepository providerRepository,
        IExternalIdentityBindingRepository bindingRepository,
        ILogger<WeComApprovalNotificationSender> logger)
        : base(registry, providerRepository, bindingRepository, logger) { }

    public override ApprovalNotificationChannel SupportedChannel => ApprovalNotificationChannel.WeCom;

    protected override ConnectorProviderType TargetProviderType => ConnectorProviderType.WeCom;
}

public sealed class FeishuApprovalNotificationSender : ExternalMessagingApprovalNotificationSenderBase
{
    public FeishuApprovalNotificationSender(
        IConnectorRegistry registry,
        IExternalIdentityProviderRepository providerRepository,
        IExternalIdentityBindingRepository bindingRepository,
        ILogger<FeishuApprovalNotificationSender> logger)
        : base(registry, providerRepository, bindingRepository, logger) { }

    public override ApprovalNotificationChannel SupportedChannel => ApprovalNotificationChannel.Feishu;

    protected override ConnectorProviderType TargetProviderType => ConnectorProviderType.Feishu;
}

public sealed class DingTalkApprovalNotificationSender : ExternalMessagingApprovalNotificationSenderBase
{
    public DingTalkApprovalNotificationSender(
        IConnectorRegistry registry,
        IExternalIdentityProviderRepository providerRepository,
        IExternalIdentityBindingRepository bindingRepository,
        ILogger<DingTalkApprovalNotificationSender> logger)
        : base(registry, providerRepository, bindingRepository, logger) { }

    public override ApprovalNotificationChannel SupportedChannel => ApprovalNotificationChannel.DingTalk;

    protected override ConnectorProviderType TargetProviderType => ConnectorProviderType.DingTalk;
}
