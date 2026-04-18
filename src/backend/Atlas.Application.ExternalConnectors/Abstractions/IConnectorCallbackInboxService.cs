namespace Atlas.Application.ExternalConnectors.Abstractions;

/// <summary>
/// 入站事件处理：验签 + 解密 + 幂等去重 + 落库 + 回流到本地审批 / 通讯录。
/// </summary>
public interface IConnectorCallbackInboxService
{
    Task<ConnectorCallbackInboxResult> AcceptAsync(long providerId, string topic, IReadOnlyDictionary<string, string> query, IReadOnlyDictionary<string, string> headers, byte[] body, CancellationToken cancellationToken);
}

public sealed class ConnectorCallbackInboxResult
{
    public required string Status { get; init; }

    public string? IdempotencyKey { get; init; }

    public string? Topic { get; init; }

    public string? Reason { get; init; }
}
