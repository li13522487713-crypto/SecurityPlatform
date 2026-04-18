using Atlas.Application.ExternalConnectors.Models;

namespace Atlas.Application.ExternalConnectors.Abstractions;

/// <summary>
/// 把"已绑定本地用户 → 签发 JWT"的能力抽到接口，避免 Application.ExternalConnectors 反向依赖具体的 JwtAuthTokenService。
/// 由 PlatformHost / Infrastructure 提供实现。
/// </summary>
public interface IConnectorJwtIssuer
{
    Task<ConnectorJwtIssueResult> IssueAsync(long localUserId, string sourceProvider, CancellationToken cancellationToken);
}

public sealed record ConnectorJwtIssueResult
{
    public required string AccessToken { get; init; }

    public string? RefreshToken { get; init; }

    public DateTimeOffset ExpiresAt { get; init; }
}
