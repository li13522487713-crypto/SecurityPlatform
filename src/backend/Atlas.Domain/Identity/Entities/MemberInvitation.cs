using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.Identity.Entities;

/// <summary>
/// 治理 M-G06-C1（S11）：成员邀请（邮件 + token）。
/// 状态机：Pending -> Accepted | Revoked | Expired。
/// </summary>
[SugarTable("MemberInvitation")]
public sealed class MemberInvitation : TenantEntity
{
    public const string StatusPending = "pending";
    public const string StatusAccepted = "accepted";
    public const string StatusRevoked = "revoked";
    public const string StatusExpired = "expired";

    public MemberInvitation()
        : base(TenantId.Empty)
    {
        Email = string.Empty;
        Token = string.Empty;
        OrganizationId = 0;
        RoleCode = "member";
        Status = StatusPending;
        CreatedAt = DateTime.UtcNow;
        ExpiresAt = CreatedAt.AddDays(7);
    }

    public MemberInvitation(
        TenantId tenantId,
        string email,
        string token,
        long organizationId,
        string roleCode,
        long invitedBy,
        DateTime expiresAt,
        long id)
        : base(tenantId)
    {
        Id = id;
        Email = email.Trim().ToLowerInvariant();
        Token = token.Trim();
        OrganizationId = organizationId;
        RoleCode = string.IsNullOrWhiteSpace(roleCode) ? "member" : roleCode.Trim();
        InvitedBy = invitedBy;
        Status = StatusPending;
        CreatedAt = DateTime.UtcNow;
        ExpiresAt = expiresAt;
    }

    [SugarColumn(Length = 256, IsNullable = false)]
    public string Email { get; private set; }

    [SugarColumn(Length = 128, IsNullable = false)]
    public string Token { get; private set; }

    public long OrganizationId { get; private set; }

    [SugarColumn(Length = 32, IsNullable = false)]
    public string RoleCode { get; private set; }

    public long InvitedBy { get; private set; }

    [SugarColumn(Length = 16, IsNullable = false)]
    public string Status { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? AcceptedAt { get; private set; }

    [SugarColumn(IsNullable = true)]
    public long? AcceptedByUserId { get; private set; }

    /// <summary>
    /// 治理 R1-B1：邀请接受后，由邀请服务分发的"设置密码"一次性令牌（base64url, 推荐 32 byte）。
    /// 用于无密码完成注册的安全交付：用户用此 token 在 /api/v1/member-invitations/set-password 设置正式密码并激活账号。
    /// 设置成功后立即 <see cref="MarkPasswordSet"/> 失效；过期后由 <see cref="IsSetPasswordTokenValid"/> 拒绝。
    /// </summary>
    [SugarColumn(Length = 256, IsNullable = true)]
    public string? SetPasswordToken { get; private set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? SetPasswordTokenExpiresAt { get; private set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? PasswordSetAt { get; private set; }

    public void MarkAccepted(long userId)
    {
        Status = StatusAccepted;
        AcceptedAt = DateTime.UtcNow;
        AcceptedByUserId = userId;
    }

    public void MarkRevoked() => Status = StatusRevoked;

    public bool IsExpired => DateTime.UtcNow > ExpiresAt;

    /// <summary>治理 R1-B1：发布一次性 set-password 令牌（覆盖之前未消费的）。</summary>
    public void IssueSetPasswordToken(string token, DateTime expiresAt)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("Token must be provided.", nameof(token));
        }
        SetPasswordToken = token.Trim();
        SetPasswordTokenExpiresAt = expiresAt;
        PasswordSetAt = null;
    }

    /// <summary>治理 R1-B1：set-password 完成后清空令牌并标记激活时间。</summary>
    public void MarkPasswordSet()
    {
        SetPasswordToken = null;
        SetPasswordTokenExpiresAt = null;
        PasswordSetAt = DateTime.UtcNow;
    }

    public bool IsSetPasswordTokenValid()
    {
        if (string.IsNullOrWhiteSpace(SetPasswordToken)) return false;
        if (!SetPasswordTokenExpiresAt.HasValue) return false;
        return DateTime.UtcNow <= SetPasswordTokenExpiresAt.Value;
    }
}
