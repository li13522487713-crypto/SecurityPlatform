using Atlas.Core.Models;

namespace Atlas.Application.Platform.Models;

public sealed record WorkspaceListItem(
    string Id,
    string OrgId,
    string Name,
    string? Description,
    string? Icon,
    string AppInstanceId,
    string AppKey,
    string RoleCode,
    int AppCount,
    int AgentCount,
    int WorkflowCount,
    string CreatedAt,
    string? LastVisitedAt);

public sealed record WorkspaceDetailDto(
    string Id,
    string OrgId,
    string Name,
    string? Description,
    string? Icon,
    string AppInstanceId,
    string AppKey,
    string RoleCode,
    IReadOnlyList<string> AllowedActions,
    string CreatedAt,
    string? LastVisitedAt);

public sealed record WorkspaceCreateRequest(
    string Name,
    string? Description,
    string? Icon,
    string AppInstanceId);

public sealed record WorkspaceUpdateRequest(
    string Name,
    string? Description,
    string? Icon);

public sealed record WorkspaceAppCardDto(
    string AppId,
    string Name,
    string? Description,
    string Status,
    string PublishStatus,
    string? Icon,
    string UpdatedAt,
    string EntryRoute,
    string? WorkflowId);

public sealed record WorkspaceAppCreateRequest(
    string Name,
    string? Description,
    string? Icon);

public sealed record WorkspaceAppCreateResult(
    string AppId,
    string WorkflowId,
    string EntryRoute);

public sealed record WorkspaceResourceCardDto(
    string ResourceType,
    string ResourceId,
    string Name,
    string? Description,
    string Status,
    string PublishStatus,
    string UpdatedAt,
    string EntryRoute,
    string? Badge,
    string? LinkedWorkflowId);

public sealed record WorkspaceMemberDto(
    string UserId,
    string Username,
    string DisplayName,
    string RoleId,
    string RoleCode,
    string RoleName,
    string JoinedAt);

public sealed record WorkspaceMemberCreateRequest(
    string UserId,
    string RoleCode);

public sealed record WorkspaceMemberRoleUpdateRequest(
    string RoleCode);

public sealed record WorkspaceRolePermissionDto(
    string RoleId,
    string RoleCode,
    string RoleName,
    IReadOnlyList<string> Actions);

public sealed record WorkspaceRolePermissionUpdateItem(
    string RoleCode,
    IReadOnlyList<string> Actions);

public sealed record WorkspaceResourcePermissionUpdateRequest(
    IReadOnlyList<WorkspaceRolePermissionUpdateItem> Items);
