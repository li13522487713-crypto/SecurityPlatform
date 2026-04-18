using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Identity.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;
using Atlas.Infrastructure.Repositories;
using SqlSugar;

namespace Atlas.Infrastructure.Services;

public sealed class OffboardService : IOffboardService
{
    private readonly ISqlSugarClient _db;
    private readonly ResourceOwnershipTransferRepository _transferRepository;
    private readonly OrganizationRepository _organizationRepository;
    private readonly OrganizationMemberRepository _memberRepository;
    private readonly IPermissionDecisionService _pdp;
    private readonly IIdGeneratorAccessor _idGenerator;

    public OffboardService(
        ISqlSugarClient db,
        ResourceOwnershipTransferRepository transferRepository,
        OrganizationRepository organizationRepository,
        OrganizationMemberRepository memberRepository,
        IPermissionDecisionService pdp,
        IIdGeneratorAccessor idGenerator)
    {
        _db = db;
        _transferRepository = transferRepository;
        _organizationRepository = organizationRepository;
        _memberRepository = memberRepository;
        _pdp = pdp;
        _idGenerator = idGenerator;
    }

    public async Task<IReadOnlyList<ResourceOwnershipTransferDto>> ExecuteOffboardAsync(
        TenantId tenantId,
        long actorUserId,
        OffboardRequest request,
        CancellationToken cancellationToken)
    {
        if (request.FromUserId <= 0 || request.ToUserId <= 0 || request.FromUserId == request.ToUserId)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "InvalidOffboardUsers");
        }
        if (request.Items is null || request.Items.Count == 0)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "OffboardItemsRequired");
        }

        var results = new List<ResourceOwnershipTransferDto>();
        foreach (var item in request.Items)
        {
            if (string.IsNullOrWhiteSpace(item.ResourceType) || string.IsNullOrWhiteSpace(item.ResourceId))
            {
                continue;
            }
            if (!long.TryParse(item.ResourceId, out var resourceIdLong))
            {
                continue;
            }
            var transfer = new ResourceOwnershipTransfer(
                tenantId,
                item.ResourceType,
                resourceIdLong,
                request.FromUserId,
                request.ToUserId,
                actorUserId,
                request.Notes,
                _idGenerator.NextId());
            transfer.MarkExecuted();
            await _transferRepository.AddAsync(transfer, cancellationToken);
            await _pdp.InvalidateResourceAsync(tenantId, item.ResourceType, resourceIdLong, cancellationToken);
            results.Add(ToDto(transfer));
        }

        // 标记 FromUser 为 offboarded
        var tenantValue = tenantId.Value;
        var fromUserId = request.FromUserId;
        var fromUser = await _db.Queryable<UserAccount>()
            .Where(u => u.TenantIdValue == tenantValue && u.Id == fromUserId)
            .FirstAsync(cancellationToken);
        if (fromUser is not null && fromUser.Status != UserAccount.StatusOffboarded)
        {
            fromUser.TransitionStatus(UserAccount.StatusOffboarded);
            await _db.Updateable(fromUser)
                .Where(u => u.Id == fromUser.Id && u.TenantIdValue == fromUser.TenantIdValue)
                .ExecuteCommandAsync(cancellationToken);
            await _pdp.InvalidateUserAsync(tenantId, fromUser.Id, cancellationToken);
        }

        return results;
    }

    public async Task MoveMemberAcrossOrganizationsAsync(
        TenantId tenantId,
        long sourceOrganizationId,
        long targetUserId,
        long actorUserId,
        OrganizationMemberMoveRequest request,
        CancellationToken cancellationToken)
    {
        if (!long.TryParse(request.TargetOrganizationId, out var targetOrgId))
        {
            throw new BusinessException(ErrorCodes.ValidationError, "InvalidTargetOrganizationId");
        }
        if (sourceOrganizationId == targetOrgId)
        {
            return;
        }
        var source = await _organizationRepository.FindByIdAsync(tenantId, sourceOrganizationId, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, "SourceOrganizationNotFound");
        var target = await _organizationRepository.FindByIdAsync(tenantId, targetOrgId, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, "TargetOrganizationNotFound");

        var existing = await _memberRepository.FindAsync(tenantId, source.Id, targetUserId, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, "OrganizationMemberNotFound");
        var roleCode = string.IsNullOrWhiteSpace(request.RoleCode) ? existing.RoleCode : request.RoleCode!;

        await _memberRepository.DeleteAsync(tenantId, source.Id, targetUserId, cancellationToken);
        var existingTarget = await _memberRepository.FindAsync(tenantId, target.Id, targetUserId, cancellationToken);
        if (existingTarget is null)
        {
            var membership = new OrganizationMember(tenantId, target.Id, targetUserId, roleCode, actorUserId, _idGenerator.NextId());
            await _memberRepository.AddAsync(membership, cancellationToken);
        }
        else
        {
            existingTarget.ChangeRole(roleCode);
            await _memberRepository.UpdateAsync(existingTarget, cancellationToken);
        }
        await _pdp.InvalidateUserAsync(tenantId, targetUserId, cancellationToken);
    }

    internal static ResourceOwnershipTransferDto ToDto(ResourceOwnershipTransfer entity)
    {
        return new ResourceOwnershipTransferDto(
            Id: entity.Id.ToString(),
            ResourceType: entity.ResourceType,
            ResourceId: entity.ResourceId.ToString(),
            FromUserId: entity.FromUserId,
            ToUserId: entity.ToUserId,
            Status: entity.Status,
            Notes: string.IsNullOrEmpty(entity.Notes) ? null : entity.Notes,
            CreatedAt: new DateTimeOffset(DateTime.SpecifyKind(entity.CreatedAt, DateTimeKind.Utc)),
            ExecutedAt: entity.ExecutedAt.HasValue
                ? new DateTimeOffset(DateTime.SpecifyKind(entity.ExecutedAt.Value, DateTimeKind.Utc))
                : null);
    }
}
