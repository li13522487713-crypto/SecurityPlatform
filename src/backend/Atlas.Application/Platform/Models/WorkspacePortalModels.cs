using Atlas.Core.Models;

namespace Atlas.Application.Platform.Models;

public sealed record WorkspaceListItem(
    string Id,
    string OrgId,
    string Name,
    string? Description,
    string? Icon,
    string? AppInstanceId,
    string? AppKey,
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
    string? AppInstanceId,
    string? AppKey,
    string RoleCode,
    IReadOnlyList<string> AllowedActions,
    string CreatedAt,
    string? LastVisitedAt);

public sealed record WorkspaceCreateRequest(
    string Name,
    string? Description,
    string? Icon);

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

/// <summary>
/// 1→N 模型：在工作空间内创建一个新的 AppManifest（应用实例）。
/// AppKey 可空，缺省时由 Service 层自动生成（如 "ws-{workspaceId}-app-{seq}"）。
/// </summary>
public sealed record WorkspaceAppInstanceCreateRequest(
    string Name,
    string? Description,
    string? Icon,
    string? Category,
    string? AppKey);

public sealed record WorkspaceAppInstanceDto(
    string AppInstanceId,
    string AppKey,
    string Name,
    string? Description,
    string? Icon,
    string? Category,
    string Status,
    int Version,
    string CreatedAt,
    string UpdatedAt);
