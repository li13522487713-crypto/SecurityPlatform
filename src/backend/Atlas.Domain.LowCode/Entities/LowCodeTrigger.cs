using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.LowCode.Entities;

/// <summary>
/// 低代码触发器（M12）。三种 Kind：cron / event / webhook。
/// </summary>
public sealed class LowCodeTrigger : TenantEntity
{
#pragma warning disable CS8618
    public LowCodeTrigger() : base(TenantId.Empty)
    {
        TriggerId = string.Empty;
        Name = string.Empty;
        Kind = "cron";
    }
#pragma warning restore CS8618

    public LowCodeTrigger(TenantId tenantId, long id, string triggerId, string name, string kind, long createdByUserId)
        : base(tenantId)
    {
        Id = id;
        TriggerId = triggerId;
        Name = name;
        Kind = kind;
        Enabled = true;
        CreatedByUserId = createdByUserId;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    [SugarColumn(Length = 64, IsNullable = false)]
    public string TriggerId { get; private set; }

    [SugarColumn(Length = 200, IsNullable = false)]
    public string Name { get; private set; }

    /// <summary>cron / event / webhook。</summary>
    [SugarColumn(Length = 32, IsNullable = false)]
    public string Kind { get; private set; }

    [SugarColumn(Length = 128, IsNullable = true)]
    public string? Cron { get; private set; }

    [SugarColumn(Length = 128, IsNullable = true)]
    public string? EventName { get; private set; }

    [SugarColumn(Length = 128, IsNullable = true)]
    public string? WebhookSecret { get; private set; }

    [SugarColumn(Length = 128, IsNullable = true)]
    public string? WorkflowId { get; private set; }

    [SugarColumn(Length = 128, IsNullable = true)]
    public string? ChatflowId { get; private set; }

    public bool Enabled { get; private set; }

    public long CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? LastFiredAt { get; private set; }

    public void Update(string name, string kind, string? cron, string? eventName, string? workflowId, string? chatflowId, bool enabled)
    {
        Name = name;
        Kind = kind;
        Cron = cron;
        EventName = eventName;
        WorkflowId = workflowId;
        ChatflowId = chatflowId;
        Enabled = enabled;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetEnabled(bool enabled)
    {
        Enabled = enabled;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void RecordFire()
    {
        LastFiredAt = DateTimeOffset.UtcNow;
    }

    /// <summary>设置 webhook 共享密钥（用于校验外部回调请求）。</summary>
    public void SetWebhookSecret(string? secret)
    {
        WebhookSecret = secret;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}

/// <summary>
/// Webview 域名白名单（M12 + M17）。
/// </summary>
public sealed class LowCodeWebviewDomain : TenantEntity
{
#pragma warning disable CS8618
    public LowCodeWebviewDomain() : base(TenantId.Empty)
    {
        Domain = string.Empty;
        VerificationToken = string.Empty;
        VerificationKind = "dns_txt";
    }
#pragma warning restore CS8618

    public LowCodeWebviewDomain(TenantId tenantId, long id, string domain, string verificationKind, string verificationToken, long createdByUserId)
        : base(tenantId)
    {
        Id = id;
        Domain = domain;
        VerificationKind = verificationKind;
        VerificationToken = verificationToken;
        CreatedByUserId = createdByUserId;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    [SugarColumn(Length = 256, IsNullable = false)]
    public string Domain { get; private set; }

    /// <summary>dns_txt / http_file。</summary>
    [SugarColumn(Length = 32, IsNullable = false)]
    public string VerificationKind { get; private set; }

    [SugarColumn(Length = 128, IsNullable = false)]
    public string VerificationToken { get; private set; }

    public bool Verified { get; private set; }
    public long CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? VerifiedAt { get; private set; }

    public void MarkVerified()
    {
        Verified = true;
        VerifiedAt = DateTimeOffset.UtcNow;
    }

    public void Revoke()
    {
        Verified = false;
        VerifiedAt = null;
    }
}
