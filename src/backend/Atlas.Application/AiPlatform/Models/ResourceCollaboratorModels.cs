using System.ComponentModel.DataAnnotations;

namespace Atlas.Application.AiPlatform.Models;

/// <summary>
/// 治理 M-G03-C6/C7（S7）：通用资源协作者 DTO。
/// 一个 (workspaceId, resourceType, resourceId) 上的协作者 = workspace 成员 + 其工作空间角色 + 资源级 ACL 覆写。
/// </summary>
public sealed record ResourceCollaboratorDto(
    string WorkspaceId,
    string ResourceType,
    string ResourceId,
    string UserId,
    string DisplayName,
    string Username,
    string RoleCode,
    bool HasExplicitResourceAcl,
    string? ExplicitActionsJson,
    DateTimeOffset JoinedAt);

public sealed record ResourceCollaboratorAddRequest(
    [Required] string UserId,
    [Required, StringLength(64, MinimumLength = 1)] string RoleCode);

public sealed record ResourceCollaboratorUpdateRequest(
    [Required, StringLength(64, MinimumLength = 1)] string RoleCode);
