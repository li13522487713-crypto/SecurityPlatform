using Atlas.Application.Audit.Abstractions;
using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Identity.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Audit.Entities;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services.Sso;

public sealed class SsoLoginPolicyService : ISsoLoginPolicyService
{
    private readonly IOrganizationService _organizationService;
    private readonly IOrganizationMemberService _memberService;
    private readonly IAuditWriter _auditWriter;
    private readonly ILogger<SsoLoginPolicyService> _logger;

    public SsoLoginPolicyService(
        IOrganizationService organizationService,
        IOrganizationMemberService memberService,
        IAuditWriter auditWriter,
        ILogger<SsoLoginPolicyService> logger)
    {
        _organizationService = organizationService;
        _memberService = memberService;
        _auditWriter = auditWriter;
        _logger = logger;
    }

    public async Task ApplyAsync(
        TenantId tenantId,
        long userId,
        string? roleCode,
        string idpType,
        string idpCode,
        CancellationToken cancellationToken)
    {
        if (userId <= 0)
        {
            _logger.LogWarning("SSO login policy invoked without userId; skipping.");
            return;
        }

        var defaultOrg = await _organizationService.GetOrCreateDefaultAsync(tenantId, userId, cancellationToken);
        var organizationIdLong = long.Parse(defaultOrg.Id);
        var role = string.IsNullOrWhiteSpace(roleCode) ? "member" : roleCode!;
        await _memberService.AddAsync(tenantId, organizationIdLong, actorUserId: userId,
            new OrganizationMemberAddRequest(userId.ToString(), role), cancellationToken);

        try
        {
            var record = new AuditRecord(
                tenantId,
                actor: userId.ToString(),
                action: "SSO_LOGIN",
                result: "success",
                target: $"idp:{idpType}/{idpCode}",
                ipAddress: null,
                userAgent: null);
            record.WithResource("organization", defaultOrg.Id);
            await _auditWriter.WriteAsync(record, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write SSO_LOGIN audit (user={UserId} idp={IdpType}/{IdpCode})", userId, idpType, idpCode);
        }
    }
}
