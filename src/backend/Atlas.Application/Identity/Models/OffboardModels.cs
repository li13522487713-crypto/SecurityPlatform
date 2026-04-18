using System.ComponentModel.DataAnnotations;

namespace Atlas.Application.Identity.Models;

public sealed record ResourceOwnershipTransferDto(
    string Id,
    string ResourceType,
    string ResourceId,
    long FromUserId,
    long ToUserId,
    string Status,
    string? Notes,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExecutedAt);

/// <summary>批量资产移交项。</summary>
public sealed record OffboardTransferItem(
    [Required] string ResourceType,
    [Required] string ResourceId);

public sealed record OffboardRequest(
    [Required] long FromUserId,
    [Required] long ToUserId,
    [Required, MinLength(1)] IReadOnlyList<OffboardTransferItem> Items,
    [StringLength(512)] string? Notes);

public sealed record OrganizationMemberMoveRequest(
    [Required] string TargetOrganizationId,
    [StringLength(32)] string? RoleCode);
