using System.ComponentModel.DataAnnotations;

namespace Atlas.Application.Identity.Models;

public sealed record OrganizationMemberDto(
    string Id,
    string OrganizationId,
    string UserId,
    string RoleCode,
    long AddedBy,
    DateTimeOffset JoinedAt,
    DateTimeOffset UpdatedAt);

public sealed record OrganizationMemberAddRequest(
    [Required] string UserId,
    [Required, StringLength(32, MinimumLength = 1)] string RoleCode);

public sealed record OrganizationMemberUpdateRequest(
    [Required, StringLength(32, MinimumLength = 1)] string RoleCode);

/// <summary>跨组织迁移请求（M-G05-C5）：把 workspace 从源组织迁到目标组织。</summary>
public sealed record WorkspaceMoveRequest(
    [Required] string TargetOrganizationId);
