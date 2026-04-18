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
    DateTimeOffset? AcceptedAt);

public sealed record MemberInvitationCreateRequest(
    [Required, EmailAddress, StringLength(256)] string Email,
    [Required] string OrganizationId,
    [StringLength(32, MinimumLength = 1)] string? RoleCode,
    int? ExpiresInDays);

public sealed record MemberInvitationAcceptRequest(
    [Required] string Token,
    long? UserId);
