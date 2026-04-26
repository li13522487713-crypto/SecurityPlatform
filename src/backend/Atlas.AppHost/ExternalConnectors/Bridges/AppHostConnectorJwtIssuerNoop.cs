using Atlas.Application.ExternalConnectors.Abstractions;

namespace Atlas.AppHost.ExternalConnectors.Bridges;

/// <summary>
/// AppHost 不暴露 OAuth 回调入口，也不应签发 JWT；
/// 但 ConnectorOAuthFlowService 在 DI 中无条件依赖 IConnectorJwtIssuer，所以注入一个 throw-on-call 的占位实现，
/// 保证容器可构造、且任何误调用都会立刻暴露而不是静默错位。
/// 当前 AppHost 未启用连接器 OAuth 回调 JWT 签发；误调用需立即暴露。
/// </summary>
public sealed class AppHostConnectorJwtIssuerNoop : IConnectorJwtIssuer
{
    public Task<ConnectorJwtIssueResult> IssueAsync(long localUserId, string sourceProvider, CancellationToken cancellationToken)
        => throw new NotSupportedException(
            "Connector JWT issuance is not enabled in this AppHost process; OAuth callback endpoints are unavailable.");
}
