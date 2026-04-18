using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.ExternalConnectors.Enums;

namespace Atlas.Domain.ExternalConnectors.Entities;

/// <summary>
/// 消息派发记录：每发送一条消息（文本 / 卡片）落一行，便于审计与卡片更新。
/// </summary>
public sealed class ExternalMessageDispatch : TenantEntity
{
    public ExternalMessageDispatch()
        : base(TenantId.Empty)
    {
        BusinessKey = string.Empty;
        RecipientJson = string.Empty;
        PayloadJson = string.Empty;
    }

    public ExternalMessageDispatch(
        TenantId tenantId,
        long id,
        long providerId,
        string businessKey,
        string recipientJson,
        string payloadJson,
        bool isCard,
        DateTimeOffset now)
        : base(tenantId)
    {
        Id = id;
        ProviderId = providerId;
        BusinessKey = businessKey;
        RecipientJson = recipientJson;
        PayloadJson = payloadJson;
        IsCard = isCard;
        Status = MessageDispatchStatus.Pending;
        CardVersion = 1;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public long ProviderId { get; private set; }

    /// <summary>业务关联键（通常是 ApprovalInstanceId / TaskId / ConversationId 等）。</summary>
    public string BusinessKey { get; private set; }

    public string RecipientJson { get; private set; }

    public string PayloadJson { get; private set; }

    public bool IsCard { get; private set; }

    public MessageDispatchStatus Status { get; private set; }

    public string? MessageId { get; private set; }

    public string? ResponseCode { get; private set; }

    public int CardVersion { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public string? ErrorMessage { get; private set; }

    public void MarkSent(string messageId, string? responseCode, DateTimeOffset now)
    {
        MessageId = messageId;
        ResponseCode = responseCode;
        Status = MessageDispatchStatus.Sent;
        UpdatedAt = now;
    }

    public void MarkUpdated(string? responseCode, int newCardVersion, DateTimeOffset now)
    {
        ResponseCode = responseCode;
        CardVersion = newCardVersion;
        Status = MessageDispatchStatus.Updated;
        UpdatedAt = now;
    }

    public void MarkFailed(string errorMessage, DateTimeOffset now)
    {
        ErrorMessage = errorMessage;
        Status = MessageDispatchStatus.Failed;
        UpdatedAt = now;
    }

    public void MarkRecalled(DateTimeOffset now)
    {
        Status = MessageDispatchStatus.Recalled;
        UpdatedAt = now;
    }
}
