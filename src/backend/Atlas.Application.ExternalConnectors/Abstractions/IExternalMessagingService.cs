using Atlas.Connectors.Core.Abstractions;
using Atlas.Connectors.Core.Models;

namespace Atlas.Application.ExternalConnectors.Abstractions;

/// <summary>
/// 外部消息派发应用服务（控制器层入口）：
/// - 调度 IExternalMessagingProvider 发文本/卡片；
/// - 强制把派发结果写 ExternalMessageDispatch 表（可溯源 / 卡片更新链路）；
/// - 卡片更新时按 dispatchId 取出上一条记录，复用 ResponseCode/MessageId。
/// </summary>
public interface IExternalMessagingService
{
    Task<ExternalMessageDispatchSummary> SendAsync(SendExternalMessageRequest request, CancellationToken cancellationToken);

    Task<ExternalMessageDispatchSummary> UpdateCardAsync(long dispatchId, UpdateCardRequest request, CancellationToken cancellationToken);
}

public sealed class SendExternalMessageRequest
{
    public required long ProviderId { get; init; }

    public required string BusinessKey { get; init; }

    public required ExternalMessageRecipient Recipient { get; init; }

    /// <summary>当 <see cref="Card"/> 为 null 时按文本发送；当 <see cref="Card"/> 非 null 时按卡片发送，<see cref="Text"/> 用作回退。</summary>
    public string? Text { get; init; }

    public ExternalMessageCard? Card { get; init; }
}

public sealed class UpdateCardRequest
{
    public required ExternalMessageCard Card { get; init; }
}

public sealed class ExternalMessageDispatchSummary
{
    public long DispatchId { get; init; }

    public required string Status { get; init; }

    public string? MessageId { get; init; }

    public string? ResponseCode { get; init; }

    public int CardVersion { get; init; }

    public string? ProviderType { get; init; }

    public string? ErrorMessage { get; init; }
}
