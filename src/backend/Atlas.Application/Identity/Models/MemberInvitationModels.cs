using System.ComponentModel.DataAnnotations;

namespace Atlas.Application.Identity.Models;

public sealed record MemberInvitationDto(
    string Id,
    string Email,
    string OrganizationId,
    string RoleCode,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? AcceptedAt,
    DateTimeOffset? PasswordSetAt = null);

public sealed record MemberInvitationCreateRequest(
    [Required, EmailAddress, StringLength(256)] string Email,
    [Required] string OrganizationId,
    [StringLength(32, MinimumLength = 1)] string? RoleCode,
    int? ExpiresInDays);

public sealed record MemberInvitationAcceptRequest(
    [Required] string Token,
    long? UserId);

/// <summary>
/// 治理 R1-B1：邀请被接受时返回的载荷。
/// 当系统为该邀请自动创建了未激活账号（pending-activation），
/// 调用方应把 <see cref="SetPasswordToken"/> 携带到 set-password 链接中（24h 有效）。
/// </summary>
public sealed record MemberInvitationAcceptResponse(
    MemberInvitationDto Invitation,
    long UserId,
    string? SetPasswordToken);

public sealed record MemberInvitationSetPasswordRequest(
    [Required, StringLength(256)] string Token,
    [Required, StringLength(128, MinimumLength = 6)] string NewPassword);
