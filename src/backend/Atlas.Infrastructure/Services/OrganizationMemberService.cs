using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Identity.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;
using Atlas.Infrastructure.Repositories;

namespace Atlas.Infrastructure.Services;

public sealed class OrganizationMemberService : IOrganizationMemberService
{
    private readonly OrganizationRepository _organizationRepository;
    private readonly OrganizationMemberRepository _memberRepository;
    private readonly WorkspaceRepository _workspaceRepository;
    private readonly IIdGeneratorAccessor _idGenerator;
    private readonly Application.Identity.Abstractions.IPermissionDecisionService _pdp;

    public OrganizationMemberService(
        OrganizationRepository organizationRepository,
        OrganizationMemberRepository memberRepository,
        WorkspaceRepository workspaceRepository,
        IIdGeneratorAccessor idGenerator,
        Application.Identity.Abstractions.IPermissionDecisionService pdp)
    {
        _organizationRepository = organizationRepository;
        _memberRepository = memberRepository;
        _workspaceRepository = workspaceRepository;
        _idGenerator = idGenerator;
        _pdp = pdp;
    }

    public async Task<IReadOnlyList<OrganizationMemberDto>> ListAsync(TenantId tenantId, long organizationId, CancellationToken cancellationToken)
    {
        await EnsureOrganizationAsync(tenantId, organizationId, cancellationToken);
        var entities = await _memberRepository.ListByOrganizationAsync(tenantId, organizationId, cancellationToken);
        return entities.Select(ToDto).ToArray();
    }

    public async Task AddAsync(TenantId tenantId, long organizationId, long actorUserId, OrganizationMemberAddRequest request, CancellationToken cancellationToken)
    {
        await EnsureOrganizationAsync(tenantId, organizationId, cancellationToken);
        if (!long.TryParse(request.UserId, out var userId))
        {
            throw new BusinessException(ErrorCodes.ValidationError, "InvalidUserId");
        }
        var existing = await _memberRepository.FindAsync(tenantId, organizationId, userId, cancellationToken);
        if (existing is not null)
        {
            existing.ChangeRole(request.RoleCode);
            await _memberRepository.UpdateAsync(existing, cancellationToken);
        }
        else
        {
            var entity = new OrganizationMember(tenantId, organizationId, userId, request.RoleCode, actorUserId, _idGenerator.NextId());
            await _memberRepository.AddAsync(entity, cancellationToken);
        }
        await _pdp.InvalidateUserAsync(tenantId, userId, cancellationToken);
    }

    public async Task UpdateAsync(TenantId tenantId, long organizationId, long targetUserId, OrganizationMemberUpdateRequest request, CancellationToken cancellationToken)
    {
        await EnsureOrganizationAsync(tenantId, organizationId, cancellationToken);
        var entity = await _memberRepository.FindAsync(tenantId, organizationId, targetUserId, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, "OrganizationMemberNotFound");
        entity.ChangeRole(request.RoleCode);
        await _memberRepository.UpdateAsync(entity, cancellationToken);
        await _pdp.InvalidateUserAsync(tenantId, targetUserId, cancellationToken);
    }

    public async Task RemoveAsync(TenantId tenantId, long organizationId, long targetUserId, CancellationToken cancellationToken)
    {
        await EnsureOrganizationAsync(tenantId, organizationId, cancellationToken);
        await _memberRepository.DeleteAsync(tenantId, organizationId, targetUserId, cancellationToken);
        await _pdp.InvalidateUserAsync(tenantId, targetUserId, cancellationToken);
    }

    public async Task MoveWorkspaceAsync(TenantId tenantId, long workspaceId, long targetOrganizationId, long actorUserId, CancellationToken cancellationToken)
    {
        var workspace = await _workspaceRepository.FindByIdAsync(tenantId, workspaceId, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, "WorkspaceNotFound");
        var target = await _organizationRepository.FindByIdAsync(tenantId, targetOrganizationId, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, "OrganizationNotFound");
        if (workspace.OrganizationId == target.Id)
        {
            return;
        }
        workspace.AssignOrganization(target.Id);
        await _workspaceRepository.UpdateAsync(workspace, cancellationToken);
        // 资源 ACL / member 关联仍跟随 workspace；仅 OrganizationId 字段变更。
        // PDP 失效：workspace 关联资源缓存将由后续 service 调用 InvalidateResourceAsync 处理。
    }

    private async Task EnsureOrganizationAsync(TenantId tenantId, long organizationId, CancellationToken cancellationToken)
    {
        var entity = await _organizationRepository.FindByIdAsync(tenantId, organizationId, cancellationToken);
        if (entity is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "OrganizationNotFound");
        }
    }

    internal static OrganizationMemberDto ToDto(OrganizationMember entity)
    {
        return new OrganizationMemberDto(
            Id: entity.Id.ToString(),
            OrganizationId: entity.OrganizationId.ToString(),
            UserId: entity.UserId.ToString(),
            RoleCode: entity.RoleCode,
            AddedBy: entity.AddedBy,
            JoinedAt: new DateTimeOffset(DateTime.SpecifyKind(entity.JoinedAt, DateTimeKind.Utc)),
            UpdatedAt: new DateTimeOffset(DateTime.SpecifyKind(entity.UpdatedAt, DateTimeKind.Utc)));
    }
}
