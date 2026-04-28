using Atlas.Application.ExternalConnectors.Abstractions;
using Atlas.Connectors.Core.Models;
using Atlas.Domain.ExternalConnectors.Enums;

namespace Atlas.Sdk.ConnectorPlugins.Nodes;

/// <summary>
/// "外部登录绑定" 节点：根据 external user profile 触发本地用户绑定（4 档策略）。
/// inputs:
///   - providerId: long
///   - externalUserId: string
///   - openId / unionId / mobile / email: string?
///   - strategy: "Direct" | "Mobile" | "Email" | "Manual"
/// outputs:
///   - kind: "Existing" / "AutoCreated" / "PendingConfirm" / "PendingManual" / "Conflict"
///   - bindingId: long?
///   - localUserId: long?
/// </summary>
public sealed class ExternalIdentityBindNode : IConnectorPluginNode
{
    private readonly IExternalIdentityBindingService _service;

    public ExternalIdentityBindNode(IExternalIdentityBindingService service) { _service = service; }

    public string NodeType => "external_identity_bind";
    public string DisplayName => "外部登录绑定";
    public string Category => "external_collaboration";

    public async Task<ConnectorPluginNodeResult> ExecuteAsync(ConnectorPluginNodeContext context, CancellationToken cancellationToken)
    {
        try
        {
            var providerId = Convert.ToInt64(context.Inputs.GetValueOrDefault("providerId") ?? 0);
            var externalUserId = context.Inputs.GetValueOrDefault("externalUserId")?.ToString() ?? string.Empty;
            var profile = new ExternalUserProfile
            {
                ProviderType = "wecom",
                ProviderTenantId = string.Empty,
                ExternalUserId = externalUserId,
                OpenId = context.Inputs.GetValueOrDefault("openId")?.ToString(),
                UnionId = context.Inputs.GetValueOrDefault("unionId")?.ToString(),
                Mobile = context.Inputs.GetValueOrDefault("mobile")?.ToString(),
                Email = context.Inputs.GetValueOrDefault("email")?.ToString(),
            };
            var strategy = Enum.TryParse<IdentityBindingMatchStrategy>(context.Inputs.GetValueOrDefault("strategy")?.ToString(), true, out var s)
                ? s : IdentityBindingMatchStrategy.Mobile;
            var resolution = await _service.ResolveOrAttemptBindAsync(providerId, profile, strategy, cancellationToken).ConfigureAwait(false);
            var outputs = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["kind"] = resolution.Kind.ToString(),
                ["bindingId"] = resolution.Binding?.Id,
                ["localUserId"] = resolution.Binding?.LocalUserId,
            };
            return new ConnectorPluginNodeResult { Success = true, Outputs = outputs };
        }
        catch (Exception ex)
        {
            return new ConnectorPluginNodeResult { Success = false, ErrorCode = ex.GetType().Name, ErrorMessage = ex.Message };
        }
    }
}
