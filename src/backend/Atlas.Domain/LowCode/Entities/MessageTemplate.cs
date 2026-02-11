using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.LowCode.Entities;

/// <summary>
/// 消息模板
/// </summary>
public sealed class MessageTemplate : TenantEntity
{
    public MessageTemplate() : base(TenantId.Empty) { Name = string.Empty; Channel = string.Empty; ContentTemplate = string.Empty; }

    public MessageTemplate(TenantId tenantId, string name, string channel, string eventType, string contentTemplate, string? subjectTemplate, string? description, long createdBy, long id, DateTimeOffset now) : base(tenantId)
    {
        Id = id; Name = name; Channel = channel; EventType = eventType;
        ContentTemplate = contentTemplate; SubjectTemplate = subjectTemplate; Description = description;
        IsActive = true; CreatedAt = now; UpdatedAt = now; CreatedBy = createdBy;
    }

    public string Name { get; private set; }
    public string Channel { get; private set; }
    public string? EventType { get; private set; }
    public string ContentTemplate { get; private set; }
    public string? SubjectTemplate { get; private set; }
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public long CreatedBy { get; private set; }

    public void Update(string name, string channel, string? eventType, string contentTemplate, string? subjectTemplate, string? description, DateTimeOffset now)
    {
        Name = name; Channel = channel; EventType = eventType;
        ContentTemplate = contentTemplate; SubjectTemplate = subjectTemplate;
        Description = description; UpdatedAt = now;
    }

    public void SetActive(bool isActive, DateTimeOffset now) { IsActive = isActive; UpdatedAt = now; }
}
