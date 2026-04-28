namespace Atlas.Application.ExternalConnectors.Abstractions;

/// <summary>
/// 入站事件处理：验签 + 解密 + 幂等去重 + 落库 + 回流到本地审批 / 通讯录 + 死信重试。
/// </summary>
public interface IConnectorCallbackInboxService
{
    Task<ConnectorCallbackInboxResult> AcceptAsync(long providerId, string topic, IReadOnlyDictionary<string, string> query, IReadOnlyDictionary<string, string> headers, byte[] body, CancellationToken cancellationToken);

    /// <summary>
    /// 扫描已落库但因下游处理失败而进入「Failed + NextRetryAt &lt;= now」的事件，重新喂给 ApplyEventAsync。
    /// 命中 maxRetry 仍失败将转入 DeadLetter。
    /// 返回处理掉的条数。
    /// </summary>
    Task<int> ProcessPendingRetriesAsync(int batchSize, CancellationToken cancellationToken);
}

public sealed class ConnectorCallbackInboxResult
{
    public required string Status { get; init; }

    public string? IdempotencyKey { get; init; }

    public string? Topic { get; init; }

    public string? Reason { get; init; }
}
