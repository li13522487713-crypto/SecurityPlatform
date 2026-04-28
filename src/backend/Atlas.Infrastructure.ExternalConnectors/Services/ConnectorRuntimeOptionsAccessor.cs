using Atlas.Application.ExternalConnectors.Abstractions;
using Atlas.Connectors.Core;
using Atlas.Connectors.DingTalk;
using Atlas.Connectors.Feishu;
using Atlas.Connectors.WeCom;

namespace Atlas.Infrastructure.ExternalConnectors.Services;

/// <summary>
/// 默认 Accessor 实现：按 ProviderType 派发到 typed
/// <see cref="IConnectorRuntimeOptionsResolver{TRuntimeOptions}"/>，并在同一 Scope 内对
/// (TenantId, ProviderInstanceId) 做 memoize，避免审批 fanout / OAuth 回调等场景重复查 DB / 重复解密。
///
/// Scoped 注册：与 SqlSugar 仓储 / SecretProtector 同生命周期。底层 Connectors.* Singleton 库不再
/// 反向依赖此 Scoped Accessor，由调用方在 Scope 内显式调用。
/// </summary>
public sealed class ConnectorRuntimeOptionsAccessor : IConnectorRuntimeOptionsAccessor
{
    private readonly IConnectorRuntimeOptionsResolver<WeComRuntimeOptions> _wecom;
    private readonly IConnectorRuntimeOptionsResolver<FeishuRuntimeOptions> _feishu;
    private readonly IConnectorRuntimeOptionsResolver<DingTalkRuntimeOptions> _dingtalk;

    private readonly Dictionary<(Guid TenantId, long ProviderInstanceId), object> _cache = new();

    public ConnectorRuntimeOptionsAccessor(
        IConnectorRuntimeOptionsResolver<WeComRuntimeOptions> wecom,
        IConnectorRuntimeOptionsResolver<FeishuRuntimeOptions> feishu,
        IConnectorRuntimeOptionsResolver<DingTalkRuntimeOptions> dingtalk)
    {
        _wecom = wecom;
        _feishu = feishu;
        _dingtalk = dingtalk;
    }

    public async Task<object> ResolveAsync(Guid tenantId, long providerInstanceId, string providerType, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(providerType))
        {
            throw new ConnectorException(ConnectorErrorCodes.ProviderConfigInvalid, "ProviderType is required to resolve runtime options.");
        }

        var key = (tenantId, providerInstanceId);
        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        object resolved = providerType switch
        {
            WeComConnectorMarker.ProviderType => await _wecom.ResolveAsync(tenantId, providerInstanceId, cancellationToken).ConfigureAwait(false),
            FeishuConnectorMarker.ProviderType => await _feishu.ResolveAsync(tenantId, providerInstanceId, cancellationToken).ConfigureAwait(false),
            DingTalkConnectorMarker.ProviderType => await _dingtalk.ResolveAsync(tenantId, providerInstanceId, cancellationToken).ConfigureAwait(false),
            _ => throw new ConnectorException(ConnectorErrorCodes.ProviderNotFound, $"Unknown ProviderType '{providerType}' for runtime options accessor.", providerType),
        };

        _cache[key] = resolved;
        return resolved;
    }
}
