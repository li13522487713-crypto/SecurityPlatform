using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.LowCode.Entities;

/// <summary>
/// 消息发送记录
/// </summary>
public sealed class MessageRecord : TenantEntity
{
    public MessageRecord() : base(TenantId.Empty) { Channel = string.Empty; Status = string.Empty; Content = string.Empty; }

    public MessageRecord(TenantId tenantId, long? templateId, string channel, string? recipientId, string? recipientAddress, string? subject, string content, string? eventType, long id, DateTimeOffset now) : base(tenantId)
    {
        Id = id; TemplateId = templateId; Channel = channel; RecipientId = recipientId;
        RecipientAddress = recipientAddress; Subject = subject; Content = content; EventType = eventType;
        Status = "Pending"; RetryCount = 0; CreatedAt = now;
    }

    public long? TemplateId { get; private set; }
    public string Channel { get; private set; }
    public string? RecipientId { get; private set; }
    public string? RecipientAddress { get; private set; }
    public string? Subject { get; private set; }
    public string Content { get; private set; }
    public string? EventType { get; private set; }
    public string Status { get; private set; }
    public int RetryCount { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? SentAt { get; private set; }
    public DateTimeOffset? ReadAt { get; private set; }

    public void MarkSent(DateTimeOffset now) { Status = "Sent"; SentAt = now; }
    public void MarkDelivered(DateTimeOffset now) { Status = "Delivered"; }
    public void MarkFailed(string error, DateTimeOffset now) { Status = "Failed"; ErrorMessage = error; RetryCount++; }
    public void MarkRead(DateTimeOffset now) { Status = "Read"; ReadAt = now; }
    public void ResetForRetry() { Status = "Pending"; ErrorMessage = null; }
}
