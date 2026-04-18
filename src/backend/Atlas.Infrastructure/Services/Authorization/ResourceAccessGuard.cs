using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Atlas.Application.Authorization;
using Atlas.Application.Identity.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Repositories;

namespace Atlas.Infrastructure.Services.Authorization;

/// <summary>
/// 治理 M-G03-C2：默认 ResourceAccessGuard 实现。
/// 三级合并判定：平台 RBAC → 工作空间角色 → 资源 ACL。具体规则见 <see cref="IResourceAccessGuard"/> 接口注释。
/// </summary>
public sealed class ResourceAccessGuard : IResourceAccessGuard
{
    private const string TierPlatform = "platform";
    private const string TierWorkspace = "workspace";
    private const string TierResource = "resource";

    private readonly WorkspaceRepository _workspaceRepository;
    private readonly WorkspaceMemberRepository _workspaceMemberRepository;
    private readonly WorkspaceRoleRepository _workspaceRoleRepository;
    private readonly WorkspaceResourcePermissionRepository _workspaceResourcePermissionRepository;
    private readonly IPermissionDecisionService _pdp;

    public ResourceAccessGuard(
        WorkspaceRepository workspaceRepository,
        WorkspaceMemberRepository workspaceMemberRepository,
        WorkspaceRoleRepository workspaceRoleRepository,
        WorkspaceResourcePermissionRepository workspaceResourcePermissionRepository,
        IPermissionDecisionService pdp)
    {
        _workspaceRepository = workspaceRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _workspaceRoleRepository = workspaceRoleRepository;
        _workspaceResourcePermissionRepository = workspaceResourcePermissionRepository;
        _pdp = pdp;
    }

    public async Task<ResourceAccessDecision> CheckAsync(ResourceAccessQuery query, CancellationToken cancellationToken)
    {
        if (query.WorkspaceId <= 0)
        {
            return new ResourceAccessDecision(false, "WorkspaceIdRequired", null);
        }
        if (string.IsNullOrWhiteSpace(query.Action))
        {
            return new ResourceAccessDecision(false, "ActionRequired", null);
        }

        // Tier 1: platform admin
        if (query.IsPlatformAdmin || await _pdp.IsSystemAdminAsync(query.TenantId, query.UserId, cancellationToken))
        {
            return new ResourceAccessDecision(true, null, TierPlatform);
        }

        // Workspace must exist & not archived
        var workspace = await _workspaceRepository.FindByIdAsync(query.TenantId, query.WorkspaceId, cancellationToken);
        if (workspace is null || workspace.IsArchived)
        {
            return new ResourceAccessDecision(false, "WorkspaceNotFound", null);
        }

        var member = await _workspaceMemberRepository.FindByWorkspaceAndUserAsync(query.TenantId, query.WorkspaceId, query.UserId, cancellationToken);
        if (member is null)
        {
            return new ResourceAccessDecision(false, "NotWorkspaceMember", null);
        }
        var role = await _workspaceRoleRepository.FindByIdAsync(query.TenantId, member.WorkspaceRoleId, cancellationToken);
        if (role is null)
        {
            return new ResourceAccessDecision(false, "WorkspaceRoleMissing", null);
        }

        // Tier 3 (resource ACL) — overrides tier 2 if present
        if (query.ResourceId is > 0 && !string.IsNullOrWhiteSpace(query.ResourceType))
        {
            var perms = await _workspaceResourcePermissionRepository.ListByResourceAsync(
                query.TenantId,
                query.WorkspaceId,
                query.ResourceType.Trim().ToLowerInvariant(),
                query.ResourceId.Value,
                cancellationToken);

            var match = perms.FirstOrDefault(p => p.WorkspaceRoleId == role.Id);
            if (match is not null)
            {
                var resourceActions = ParseActions(match.ActionsJson);
                if (resourceActions.Contains(query.Action, StringComparer.OrdinalIgnoreCase))
                {
                    return new ResourceAccessDecision(true, null, TierResource);
                }
                // 资源级 ACL 命中但没有该 action — 显式拒绝（不再 fallback workspace 默认 actions）。
                return new ResourceAccessDecision(false, "ResourceAclDenied", null);
            }
            // 没有该 (role, resource) 的显式 ACL 条目 → fallback 到 workspace 默认 actions。
        }

        // Tier 2: workspace role default actions
        var defaultActions = ParseActions(role.DefaultActionsJson);
        if (defaultActions.Contains(query.Action, StringComparer.OrdinalIgnoreCase))
        {
            return new ResourceAccessDecision(true, null, TierWorkspace);
        }

        return new ResourceAccessDecision(false, "WorkspaceRoleDenied", null);
    }

    public async Task RequireAsync(ResourceAccessQuery query, CancellationToken cancellationToken)
    {
        var decision = await CheckAsync(query, cancellationToken);
        if (decision.Allowed)
        {
            return;
        }
        if (decision.DeniedReason == "WorkspaceNotFound")
        {
            throw new BusinessException(ErrorCodes.NotFound, decision.DeniedReason);
        }
        throw new BusinessException(ErrorCodes.Forbidden, decision.DeniedReason ?? "AccessDenied");
    }

    internal static string[] ParseActions(string actionsJson)
    {
        if (string.IsNullOrWhiteSpace(actionsJson))
        {
            return Array.Empty<string>();
        }
        try
        {
            return JsonSerializer.Deserialize<string[]>(actionsJson) ?? Array.Empty<string>();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }
}
