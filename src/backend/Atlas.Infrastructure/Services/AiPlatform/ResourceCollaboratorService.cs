using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Application.Identity.Abstractions;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.Identity.Entities;
using Atlas.Infrastructure.Repositories;
using SqlSugar;

namespace Atlas.Infrastructure.Services.AiPlatform;

/// <summary>
/// 治理 M-G03-C7（S7）：通用资源协作者服务实现。
/// 通过 workspace 成员 + 角色映射实现「Coze 风格协作者」；资源级 ACL 覆写信息一并返回，
/// 让前端 CollaboratorDrawer 可以同时呈现「角色继承」与「资源级覆写」两种状态。
/// </summary>
public sealed class ResourceCollaboratorService : IResourceCollaboratorService
{
    private static readonly HashSet<string> AllowedResourceTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "agent", "workflow", "app", "knowledge", "database", "plugin"
    };

    private readonly ISqlSugarClient _db;
    private readonly WorkspaceRepository _workspaceRepository;
    private readonly WorkspaceMemberRepository _memberRepository;
    private readonly WorkspaceRoleRepository _roleRepository;
    private readonly WorkspaceResourcePermissionRepository _permissionRepository;
    private readonly IPermissionDecisionService _pdp;
    private readonly IIdGeneratorAccessor _idGenerator;

    public ResourceCollaboratorService(
        ISqlSugarClient db,
        WorkspaceRepository workspaceRepository,
        WorkspaceMemberRepository memberRepository,
        WorkspaceRoleRepository roleRepository,
        WorkspaceResourcePermissionRepository permissionRepository,
        IPermissionDecisionService pdp,
        IIdGeneratorAccessor idGenerator)
    {
        _db = db;
        _workspaceRepository = workspaceRepository;
        _memberRepository = memberRepository;
        _roleRepository = roleRepository;
        _permissionRepository = permissionRepository;
        _pdp = pdp;
        _idGenerator = idGenerator;
    }

    public async Task<IReadOnlyList<ResourceCollaboratorDto>> ListAsync(
        TenantId tenantId,
        long workspaceId,
        string resourceType,
        long resourceId,
        CancellationToken cancellationToken)
    {
        EnsureResourceType(resourceType);
        await EnsureWorkspaceExistsAsync(tenantId, workspaceId, cancellationToken);

        var members = await _memberRepository.ListByWorkspaceAsync(tenantId, workspaceId, cancellationToken);
        if (members.Count == 0)
        {
            return Array.Empty<ResourceCollaboratorDto>();
        }

        var roles = await _roleRepository.ListByWorkspaceAsync(tenantId, workspaceId, cancellationToken);
        var roleById = roles.ToDictionary(r => r.Id);
        var perms = await _permissionRepository.ListByResourceAsync(
            tenantId, workspaceId, resourceType.Trim().ToLowerInvariant(), resourceId, cancellationToken);
        var explicitByRoleId = perms.ToDictionary(p => p.WorkspaceRoleId);

        var userIds = members.Select(m => m.UserId).Distinct().ToArray();
        var users = await _db.Queryable<UserAccount>()
            .Where(u => u.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(userIds, u.Id))
            .ToListAsync(cancellationToken);
        var userById = users.ToDictionary(u => u.Id);

        var workspaceIdStr = workspaceId.ToString();
        var resourceIdStr = resourceId.ToString();
        var normalizedResourceType = resourceType.Trim().ToLowerInvariant();

        return members.Select(m =>
        {
            roleById.TryGetValue(m.WorkspaceRoleId, out var role);
            userById.TryGetValue(m.UserId, out var user);
            explicitByRoleId.TryGetValue(m.WorkspaceRoleId, out var explicitPerm);
            return new ResourceCollaboratorDto(
                WorkspaceId: workspaceIdStr,
                ResourceType: normalizedResourceType,
                ResourceId: resourceIdStr,
                UserId: m.UserId.ToString(),
                DisplayName: user?.DisplayName ?? user?.Username ?? string.Empty,
                Username: user?.Username ?? string.Empty,
                RoleCode: role?.Code ?? string.Empty,
                HasExplicitResourceAcl: explicitPerm is not null,
                ExplicitActionsJson: explicitPerm?.ActionsJson,
                JoinedAt: new DateTimeOffset(DateTime.SpecifyKind(m.JoinedAt, DateTimeKind.Utc)));
        }).ToArray();
    }

    public async Task AddAsync(
        TenantId tenantId,
        long workspaceId,
        string resourceType,
        long resourceId,
        long actorUserId,
        ResourceCollaboratorAddRequest request,
        CancellationToken cancellationToken)
    {
        EnsureResourceType(resourceType);
        await EnsureWorkspaceExistsAsync(tenantId, workspaceId, cancellationToken);

        if (!long.TryParse(request.UserId, out var userId))
        {
            throw new BusinessException(ErrorCodes.ValidationError, "InvalidUserId");
        }
        var role = await ResolveRoleAsync(tenantId, workspaceId, request.RoleCode, cancellationToken);

        var existing = await _memberRepository.FindByWorkspaceAndUserAsync(tenantId, workspaceId, userId, cancellationToken);
        if (existing is not null)
        {
            // already a member: update role if different
            if (existing.WorkspaceRoleId != role.Id)
            {
                // 简化：删旧加新，避免实体新增更新方法
                await _memberRepository.DeleteByWorkspaceAndUserAsync(tenantId, workspaceId, userId, cancellationToken);
                var replacement = new WorkspaceMember(tenantId, workspaceId, userId, role.Id, actorUserId, _idGenerator.NextId());
                await _memberRepository.AddAsync(replacement, cancellationToken);
            }
        }
        else
        {
            var member = new WorkspaceMember(tenantId, workspaceId, userId, role.Id, actorUserId, _idGenerator.NextId());
            await _memberRepository.AddAsync(member, cancellationToken);
        }

        await _pdp.InvalidateResourceAsync(tenantId, resourceType, resourceId, cancellationToken);
        await _pdp.InvalidateUserAsync(tenantId, userId, cancellationToken);
    }

    public async Task UpdateAsync(
        TenantId tenantId,
        long workspaceId,
        string resourceType,
        long resourceId,
        long targetUserId,
        long actorUserId,
        ResourceCollaboratorUpdateRequest request,
        CancellationToken cancellationToken)
    {
        EnsureResourceType(resourceType);
        await EnsureWorkspaceExistsAsync(tenantId, workspaceId, cancellationToken);
        var role = await ResolveRoleAsync(tenantId, workspaceId, request.RoleCode, cancellationToken);
        var existing = await _memberRepository.FindByWorkspaceAndUserAsync(tenantId, workspaceId, targetUserId, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, "CollaboratorNotFound");
        if (existing.WorkspaceRoleId == role.Id)
        {
            return;
        }
        await _memberRepository.DeleteByWorkspaceAndUserAsync(tenantId, workspaceId, targetUserId, cancellationToken);
        var replacement = new WorkspaceMember(tenantId, workspaceId, targetUserId, role.Id, actorUserId, _idGenerator.NextId());
        await _memberRepository.AddAsync(replacement, cancellationToken);
        await _pdp.InvalidateResourceAsync(tenantId, resourceType, resourceId, cancellationToken);
        await _pdp.InvalidateUserAsync(tenantId, targetUserId, cancellationToken);
    }

    public async Task RemoveAsync(
        TenantId tenantId,
        long workspaceId,
        string resourceType,
        long resourceId,
        long targetUserId,
        long actorUserId,
        CancellationToken cancellationToken)
    {
        EnsureResourceType(resourceType);
        await EnsureWorkspaceExistsAsync(tenantId, workspaceId, cancellationToken);
        await _memberRepository.DeleteByWorkspaceAndUserAsync(tenantId, workspaceId, targetUserId, cancellationToken);
        await _pdp.InvalidateResourceAsync(tenantId, resourceType, resourceId, cancellationToken);
        await _pdp.InvalidateUserAsync(tenantId, targetUserId, cancellationToken);
    }

    private async Task<WorkspaceRole> ResolveRoleAsync(TenantId tenantId, long workspaceId, string code, CancellationToken cancellationToken)
    {
        var role = await _roleRepository.FindByCodeAsync(tenantId, workspaceId, code.Trim(), cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, "WorkspaceRoleNotFound");
        return role;
    }

    private async Task EnsureWorkspaceExistsAsync(TenantId tenantId, long workspaceId, CancellationToken cancellationToken)
    {
        var workspace = await _workspaceRepository.FindByIdAsync(tenantId, workspaceId, cancellationToken);
        if (workspace is null || workspace.IsArchived)
        {
            throw new BusinessException(ErrorCodes.NotFound, "WorkspaceNotFound");
        }
    }

    private static void EnsureResourceType(string resourceType)
    {
        if (string.IsNullOrWhiteSpace(resourceType) || !AllowedResourceTypes.Contains(resourceType))
        {
            throw new BusinessException(ErrorCodes.ValidationError, "InvalidResourceType");
        }
    }
}
