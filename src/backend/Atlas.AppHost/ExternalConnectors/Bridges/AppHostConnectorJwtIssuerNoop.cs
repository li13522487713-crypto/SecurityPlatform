using Atlas.Application.ExternalConnectors.Abstractions;

namespace Atlas.AppHost.ExternalConnectors.Bridges;

/// <summary>
/// AppHost 不暴露 OAuth 回调入口，也不应签发 JWT；
/// 但 ConnectorOAuthFlowService 在 DI 中无条件依赖 IConnectorJwtIssuer，所以注入一个 throw-on-call 的占位实现，
/// 保证容器可构造、且任何误调用都会立刻暴露而不是静默错位。
/// 真正的 JWT 签发逻辑统一收敛在 PlatformHost.ConnectorJwtIssuerBridge。
/// </summary>
public sealed class AppHostConnectorJwtIssuerNoop : IConnectorJwtIssuer
{
    public Task<ConnectorJwtIssueResult> IssueAsync(long localUserId, string sourceProvider, CancellationToken cancellationToken)
        => throw new NotSupportedException(
            "Connector JWT issuance must run on PlatformHost; AppHost does not expose OAuth callback endpoints.");
}
