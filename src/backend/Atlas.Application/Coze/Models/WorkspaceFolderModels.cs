using System.ComponentModel.DataAnnotations;

namespace Atlas.Application.Coze.Models;

public sealed record WorkspaceFolderListItem(
    string Id,
    string WorkspaceId,
    string Name,
    string? Description,
    int ItemCount,
    string CreatedByDisplayName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record WorkspaceFolderCreateRequest(
    [Required, StringLength(40, MinimumLength = 1)] string Name,
    [StringLength(800)] string? Description);

public sealed record WorkspaceFolderUpdateRequest(
    [StringLength(40, MinimumLength = 1)] string? Name,
    [StringLength(800)] string? Description);

public sealed record WorkspaceFolderItemMoveRequest(
    [Required] string ItemType,
    [Required] string ItemId);
