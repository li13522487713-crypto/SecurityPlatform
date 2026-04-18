using Atlas.Application.Identity.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.Identity.Abstractions;

public interface IMemberInvitationService
{
    Task<IReadOnlyList<MemberInvitationDto>> ListAsync(TenantId tenantId, long? organizationId, CancellationToken cancellationToken);

    Task<MemberInvitationDto> CreateAsync(TenantId tenantId, long invitedBy, MemberInvitationCreateRequest request, CancellationToken cancellationToken);

    Task RevokeAsync(TenantId tenantId, long invitationId, CancellationToken cancellationToken);

    /// <summary>
    /// 治理 R1-B1：接受邀请。
    /// 若 <paramref name="request"/>.UserId 缺失且 invitation.Email 对应账号不存在 → 自动创建 UserAccount(IsActive=false, status=pending-activation)
    /// 并返回一次性 set-password token（24h），调用方需把 token 透传给前端引导页。
    /// 若用户已存在或显式提供 UserId，则直接 join org 不触发账号创建（SetPasswordToken=null）。
    /// </summary>
    Task<MemberInvitationAcceptResponse> AcceptAsync(TenantId tenantId, MemberInvitationAcceptRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// 治理 R1-B1：使用 invitation 中的 set-password 一次性 token 设置正式密码并激活账号。
    /// </summary>
    Task SetPasswordAsync(TenantId tenantId, MemberInvitationSetPasswordRequest request, CancellationToken cancellationToken);
}

public interface IInvitationEmailSender
{
    Task SendAsync(string toEmail, string token, string organizationName, CancellationToken cancellationToken);
}
