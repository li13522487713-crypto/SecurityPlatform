using System.Security.Cryptography;
using Atlas.Application.Abstractions;
using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Identity.Models;
using Atlas.Application.Options;
using Atlas.Application.Security;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;
using Atlas.Infrastructure.Repositories;
using Microsoft.Extensions.Options;

namespace Atlas.Infrastructure.Services;

public sealed class MemberInvitationService : IMemberInvitationService
{
    private static readonly TimeSpan SetPasswordTokenLifetime = TimeSpan.FromHours(24);

    private readonly MemberInvitationRepository _repository;
    private readonly OrganizationRepository _organizationRepository;
    private readonly OrganizationMemberRepository _memberRepository;
    private readonly IInvitationEmailSender _emailSender;
    private readonly IIdGeneratorAccessor _idGenerator;
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IOptionsMonitor<PasswordPolicyOptions> _passwordPolicyMonitor;

    public MemberInvitationService(
        MemberInvitationRepository repository,
        OrganizationRepository organizationRepository,
        OrganizationMemberRepository memberRepository,
        IInvitationEmailSender emailSender,
        IIdGeneratorAccessor idGenerator,
        IUserAccountRepository userAccountRepository,
        IPasswordHasher passwordHasher,
        IOptionsMonitor<PasswordPolicyOptions> passwordPolicyMonitor)
    {
        _repository = repository;
        _organizationRepository = organizationRepository;
        _memberRepository = memberRepository;
        _emailSender = emailSender;
        _idGenerator = idGenerator;
        _userAccountRepository = userAccountRepository;
        _passwordHasher = passwordHasher;
        _passwordPolicyMonitor = passwordPolicyMonitor;
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

    public async Task<MemberInvitationAcceptResponse> AcceptAsync(
        TenantId tenantId,
        MemberInvitationAcceptRequest request,
        CancellationToken cancellationToken)
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

        // 治理 R1-B1：解析或自动创建 UserAccount。
        long userId = request.UserId ?? 0L;
        string? setPasswordToken = null;

        UserAccount? user = null;
        if (userId > 0)
        {
            user = await _userAccountRepository.FindByIdAsync(tenantId, userId, cancellationToken);
            // 显式传 userId 但找不到 → 拒绝（避免错误绑定）
            if (user is null)
            {
                throw new BusinessException(ErrorCodes.NotFound, "UserNotFound");
            }
        }
        else
        {
            // 邮件已存在账号 → 直接关联
            user = await _userAccountRepository.FindByEmailAsync(tenantId, entity.Email, cancellationToken);
            if (user is null)
            {
                // 自动创建 pending-activation 账号 + set-password token
                user = await CreatePendingActivationUserAsync(tenantId, entity.Email, cancellationToken);
                setPasswordToken = GenerateToken(32);
                entity.IssueSetPasswordToken(setPasswordToken, DateTime.UtcNow.Add(SetPasswordTokenLifetime));
            }
            userId = user.Id;
        }

        entity.MarkAccepted(userId);
        await _repository.UpdateAsync(entity, cancellationToken);

        // 加入组织（即使 user 已存在 / 已是成员也保持幂等）
        if (userId > 0)
        {
            var existing = await _memberRepository.FindAsync(tenantId, entity.OrganizationId, userId, cancellationToken);
            if (existing is null)
            {
                var member = new OrganizationMember(tenantId, entity.OrganizationId, userId, entity.RoleCode, entity.InvitedBy, _idGenerator.NextId());
                await _memberRepository.AddAsync(member, cancellationToken);
            }
        }

        return new MemberInvitationAcceptResponse(ToDto(entity), userId, setPasswordToken);
    }

    public async Task SetPasswordAsync(
        TenantId tenantId,
        MemberInvitationSetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token)) throw new BusinessException(ErrorCodes.ValidationError, "TokenRequired");
        var policy = _passwordPolicyMonitor.CurrentValue;
        if (!PasswordPolicy.IsCompliant(request.NewPassword, policy, out var errorMessage))
        {
            throw new BusinessException(ErrorCodes.ValidationError, errorMessage);
        }

        var invitation = await _repository.FindBySetPasswordTokenAsync(tenantId, request.Token, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, "SetPasswordTokenNotFound");
        if (!invitation.IsSetPasswordTokenValid())
        {
            throw new BusinessException(ErrorCodes.ValidationError, "SetPasswordTokenExpired");
        }
        if (invitation.Status != MemberInvitation.StatusAccepted || !invitation.AcceptedByUserId.HasValue)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "InvitationNotAccepted");
        }

        var userId = invitation.AcceptedByUserId.Value;
        var user = await _userAccountRepository.FindByIdAsync(tenantId, userId, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, "UserNotFound");

        var hash = _passwordHasher.HashPassword(request.NewPassword);
        user.UpdatePassword(hash, DateTimeOffset.UtcNow);
        user.TransitionStatus(UserAccount.StatusActive);
        user.Activate();
        await _userAccountRepository.UpdateAsync(user, cancellationToken);

        invitation.MarkPasswordSet();
        await _repository.UpdateAsync(invitation, cancellationToken);
    }

    /// <summary>
    /// 治理 R1-B1：为接受邀请的新邮件创建 pending-activation 账号。
    /// 临时密码满足策略（默认 8 位含大小写 / 数字 / 特殊符）：用户必须用 SetPassword 流程才能登录。
    /// 直接走仓储而非 UserCommandService 以避开 RoleIds / DepartmentIds 等强校验依赖。
    /// </summary>
    private async Task<UserAccount> CreatePendingActivationUserAsync(
        TenantId tenantId,
        string email,
        CancellationToken cancellationToken)
    {
        var username = email; // 邮箱即用户名（与 OidcAccountMapper 保持一致）
        var displayName = ResolveDisplayNameFromEmail(email);
        if (await _userAccountRepository.ExistsByUsernameAsync(tenantId, username, cancellationToken))
        {
            // 同租户内已有同名账号但邮箱关联失败（极少见竞态）→ 直接复用
            var existing = await _userAccountRepository.FindByUsernameAsync(tenantId, username, cancellationToken);
            if (existing is not null) return existing;
        }

        var temporaryPassword = GenerateCompliantPassword(_passwordPolicyMonitor.CurrentValue);
        var hash = _passwordHasher.HashPassword(temporaryPassword);
        var user = new UserAccount(tenantId, username, displayName, hash, _idGenerator.NextId());
        user.UpdateProfile(displayName, email, null);
        user.Deactivate();
        user.TransitionStatus(UserAccount.StatusPendingActivation);
        await _userAccountRepository.AddAsync(user, cancellationToken);
        return user;
    }

    private static string ResolveDisplayNameFromEmail(string email)
    {
        var atIdx = email.IndexOf('@');
        if (atIdx <= 0) return email;
        var local = email.Substring(0, atIdx);
        return string.IsNullOrWhiteSpace(local) ? email : local;
    }

    /// <summary>
    /// 治理 R1-B1：生成必然合规的随机临时密码（用于 pending-activation 账号占位）。
    /// 模板：1 大写 + 1 小写 + 1 数字 + 1 符号 + 余下 base64url 随机字符。
    /// 用户必须走 SetPassword 流程才能正常登录。
    /// </summary>
    private static string GenerateCompliantPassword(PasswordPolicyOptions options)
    {
        const string Uppers = "ABCDEFGHJKMNPQRSTUVWXYZ";
        const string Lowers = "abcdefghjkmnpqrstuvwxyz";
        const string Digits = "23456789";
        const string Specials = "!@#$%^&*()-_=+";
        var minLength = options.MinLength < 8 ? 8 : options.MinLength;

        var chars = new List<char>(minLength + 4)
        {
            Pick(Uppers),
            Pick(Lowers),
            Pick(Digits),
            Pick(Specials)
        };

        while (chars.Count < minLength + 4)
        {
            chars.Add(Pick(Uppers + Lowers + Digits));
        }

        // Fisher-Yates 打乱顺序
        for (var i = chars.Count - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }
        return new string(chars.ToArray());

        static char Pick(string pool) => pool[RandomNumberGenerator.GetInt32(pool.Length)];
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
                : null,
            PasswordSetAt: entity.PasswordSetAt.HasValue
                ? new DateTimeOffset(DateTime.SpecifyKind(entity.PasswordSetAt.Value, DateTimeKind.Utc))
                : null);
    }
}
