using System.ComponentModel.DataAnnotations;

namespace Atlas.Application.Identity.Models;

/// <summary>治理 M-G05-C1（S9）：组织 DTO。</summary>
public sealed record OrganizationDto(
    string Id,
    string Code,
    string Name,
    string? Description,
    bool IsDefault,
    long CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record OrganizationCreateRequest(
    [Required, StringLength(64, MinimumLength = 1)] string Code,
    [Required, StringLength(128, MinimumLength = 1)] string Name,
    [StringLength(512)] string? Description);

public sealed record OrganizationUpdateRequest(
    [Required, StringLength(128, MinimumLength = 1)] string Name,
    [StringLength(512)] string? Description);
