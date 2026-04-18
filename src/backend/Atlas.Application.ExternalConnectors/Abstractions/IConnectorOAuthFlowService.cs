using Atlas.Application.ExternalConnectors.Models;

namespace Atlas.Application.ExternalConnectors.Abstractions;

/// <summary>
/// 把 ConnectorOAuthController 的薄包装抽出来：负责 state 生成 / consume / 调用 ExternalIdentityProvider / 串联 BindingService。
/// </summary>
public interface IConnectorOAuthFlowService
{
    Task<OAuthInitiationResponse> InitiateAsync(OAuthInitiationRequest request, CancellationToken cancellationToken);

    Task<OAuthCallbackResult> CompleteAsync(OAuthCallbackRequest request, CancellationToken cancellationToken);
}
