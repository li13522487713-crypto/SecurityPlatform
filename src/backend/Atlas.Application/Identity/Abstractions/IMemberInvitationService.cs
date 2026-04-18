using Atlas.Application.Identity.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.Identity.Abstractions;

public interface IMemberInvitationService
{
    Task<IReadOnlyList<MemberInvitationDto>> ListAsync(TenantId tenantId, long? organizationId, CancellationToken cancellationToken);

    Task<MemberInvitationDto> CreateAsync(TenantId tenantId, long invitedBy, MemberInvitationCreateRequest request, CancellationToken cancellationToken);

    Task RevokeAsync(TenantId tenantId, long invitationId, CancellationToken cancellationToken);

    Task AcceptAsync(TenantId tenantId, MemberInvitationAcceptRequest request, CancellationToken cancellationToken);
}

public interface IInvitationEmailSender
{
    Task SendAsync(string toEmail, string token, string organizationName, CancellationToken cancellationToken);
}
