using System.Security.Cryptography;
using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Identity.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;
using Atlas.Infrastructure.Repositories;

namespace Atlas.Infrastructure.Services;

public sealed class MemberInvitationService : IMemberInvitationService
{
    private readonly MemberInvitationRepository _repository;
    private readonly OrganizationRepository _organizationRepository;
    private readonly OrganizationMemberRepository _memberRepository;
    private readonly IInvitationEmailSender _emailSender;
    private readonly IIdGeneratorAccessor _idGenerator;

    public MemberInvitationService(
        MemberInvitationRepository repository,
        OrganizationRepository organizationRepository,
        OrganizationMemberRepository memberRepository,
        IInvitationEmailSender emailSender,
        IIdGeneratorAccessor idGenerator)
    {
        _repository = repository;
        _organizationRepository = organizationRepository;
        _memberRepository = memberRepository;
        _emailSender = emailSender;
        _idGenerator = idGenerator;
    }

    public async Task<IReadOnlyList<MemberInvitationDto>> ListAsync(TenantId tenantId, long? organizationId, CancellationToken cancellationToken)
    {
        var entities = await _repository.ListByTenantAsync(tenantId, organizationId, cancellationToken);
        return entities.Select(ToDto).ToArray();
    }

    public async Task<MemberInvitationDto> CreateAsync(TenantId tenantId, long invitedBy, MemberInvitationCreateRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email)) throw new BusinessException(ErrorCodes.ValidationError, "EmailRequired");
        if (!long.TryParse(request.OrganizationId, out var organizationId)) throw new BusinessException(ErrorCodes.ValidationError, "InvalidOrganizationId");
        var organization = await _organizationRepository.FindByIdAsync(tenantId, organizationId, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, "OrganizationNotFound");

        var token = GenerateToken();
        var expiresAt = DateTime.UtcNow.AddDays(request.ExpiresInDays is > 0 ? request.ExpiresInDays.Value : 7);
        var entity = new MemberInvitation(
            tenantId,
            request.Email.Trim().ToLowerInvariant(),
            token,
            organizationId,
            request.RoleCode ?? "member",
            invitedBy,
            expiresAt,
            _idGenerator.NextId());
        await _repository.AddAsync(entity, cancellationToken);
        await _emailSender.SendAsync(entity.Email, token, organization.Name, cancellationToken);
        return ToDto(entity);
    }

    public async Task RevokeAsync(TenantId tenantId, long invitationId, CancellationToken cancellationToken)
    {
        var entity = await _repository.FindByIdAsync(tenantId, invitationId, cancellationToken);
        if (entity is null) return;
        if (entity.Status != MemberInvitation.StatusPending)
        {
            return;
        }
        entity.MarkRevoked();
        await _repository.UpdateAsync(entity, cancellationToken);
    }

    public async Task AcceptAsync(TenantId tenantId, MemberInvitationAcceptRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token)) throw new BusinessException(ErrorCodes.ValidationError, "TokenRequired");
        var entity = await _repository.FindByTokenAsync(tenantId, request.Token, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, "InvitationNotFound");
        if (entity.Status != MemberInvitation.StatusPending)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "InvitationNotPending");
        }
        if (entity.IsExpired)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "InvitationExpired");
        }
        var userId = request.UserId ?? 0L;
        entity.MarkAccepted(userId);
        await _repository.UpdateAsync(entity, cancellationToken);
        if (userId > 0)
        {
            // 自动加入组织
            var existing = await _memberRepository.FindAsync(tenantId, entity.OrganizationId, userId, cancellationToken);
            if (existing is null)
            {
                var member = new OrganizationMember(tenantId, entity.OrganizationId, userId, entity.RoleCode, entity.InvitedBy, _idGenerator.NextId());
                await _memberRepository.AddAsync(member, cancellationToken);
            }
        }
    }

    private static string GenerateToken(int byteLength = 32)
    {
        var bytes = RandomNumberGenerator.GetBytes(byteLength);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    internal static MemberInvitationDto ToDto(MemberInvitation entity)
    {
        return new MemberInvitationDto(
            Id: entity.Id.ToString(),
            Email: entity.Email,
            OrganizationId: entity.OrganizationId.ToString(),
            RoleCode: entity.RoleCode,
            Status: entity.Status,
            CreatedAt: new DateTimeOffset(DateTime.SpecifyKind(entity.CreatedAt, DateTimeKind.Utc)),
            ExpiresAt: new DateTimeOffset(DateTime.SpecifyKind(entity.ExpiresAt, DateTimeKind.Utc)),
            AcceptedAt: entity.AcceptedAt.HasValue
                ? new DateTimeOffset(DateTime.SpecifyKind(entity.AcceptedAt.Value, DateTimeKind.Utc))
                : null);
    }
}
