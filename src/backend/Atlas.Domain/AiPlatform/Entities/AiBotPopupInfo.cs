using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.AiPlatform.Entities;

public sealed class AiBotPopupInfo : TenantEntity
{
    public AiBotPopupInfo()
        : base(TenantId.Empty)
    {
        PopupCode = string.Empty;
        Title = string.Empty;
        Content = string.Empty;
        CreatedAt = DateTime.UtcNow;
    }

    public AiBotPopupInfo(
        TenantId tenantId,
        long userId,
        string popupCode,
        string title,
        string content,
        bool dismissed,
        long id)
        : base(tenantId)
    {
        Id = id;
        UserId = userId;
        PopupCode = popupCode;
        Title = title;
        Content = content;
        Dismissed = dismissed;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public long UserId { get; private set; }
    public string PopupCode { get; private set; }
    public string Title { get; private set; }
    public string Content { get; private set; }
    public bool Dismissed { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public void UpdateContent(string title, string content)
    {
        Title = title;
        Content = content;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetDismissed(bool dismissed)
    {
        Dismissed = dismissed;
        UpdatedAt = DateTime.UtcNow;
    }
}
