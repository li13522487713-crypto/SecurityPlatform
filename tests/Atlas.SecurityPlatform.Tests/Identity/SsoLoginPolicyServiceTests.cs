using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Atlas.Application.Audit.Abstractions;
using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Identity.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Audit.Entities;
using Atlas.Infrastructure.Services.Sso;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Atlas.SecurityPlatform.Tests.Identity;

/// <summary>
/// 治理 R1-F4：SsoLoginPolicyService 正反例。
/// </summary>
public sealed class SsoLoginPolicyServiceTests
{
    private static readonly TenantId Tenant = new(Guid.Parse("00000000-0000-0000-0000-000000000500"));

    [Fact]
    public async Task ApplyAsync_ShouldEnsureDefaultOrg_AndAddMember_AndAudit()
    {
        var orgSvc = new RecordingOrganizationService();
        var memberSvc = new RecordingMemberService();
        var audit = new RecordingAuditWriter();
        var svc = new SsoLoginPolicyService(orgSvc, memberSvc, audit, NullLogger<SsoLoginPolicyService>.Instance);

        await svc.ApplyAsync(Tenant, userId: 9001L, roleCode: "member",
            idpType: "oidc", idpCode: "corp-ldap", CancellationToken.None);

        Assert.Equal(1, orgSvc.GetOrCreateDefaultCalls);
        Assert.Single(memberSvc.AddCalls);
        Assert.Equal("9001", memberSvc.AddCalls[0].Request.UserId);
        Assert.Single(audit.Records);
        Assert.Equal("SSO_LOGIN", audit.Records[0].Action);
        Assert.Equal("organization", audit.Records[0].ResourceType);
    }

    [Fact]
    public async Task ApplyAsync_ShouldShortCircuit_WhenUserIdInvalid()
    {
        var orgSvc = new RecordingOrganizationService();
        var memberSvc = new RecordingMemberService();
        var audit = new RecordingAuditWriter();
        var svc = new SsoLoginPolicyService(orgSvc, memberSvc, audit, NullLogger<SsoLoginPolicyService>.Instance);

        await svc.ApplyAsync(Tenant, userId: 0L, roleCode: null,
            idpType: "oidc", idpCode: "x", CancellationToken.None);

        Assert.Equal(0, orgSvc.GetOrCreateDefaultCalls);
        Assert.Empty(memberSvc.AddCalls);
        Assert.Empty(audit.Records);
    }

    private sealed class RecordingOrganizationService : IOrganizationService
    {
        public int GetOrCreateDefaultCalls;

        public Task<OrganizationDto> GetOrCreateDefaultAsync(TenantId tenantId, long createdBy, CancellationToken cancellationToken)
        {
            GetOrCreateDefaultCalls++;
            return Task.FromResult(new OrganizationDto(
                Id: "12345",
                Code: "default",
                Name: "Default",
                Description: null,
                IsDefault: true,
                CreatedBy: createdBy,
                CreatedAt: DateTimeOffset.UtcNow,
                UpdatedAt: DateTimeOffset.UtcNow));
        }

        public Task<IReadOnlyList<OrganizationDto>> ListAsync(TenantId tenantId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<OrganizationDto?> GetByCodeAsync(TenantId tenantId, string code, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<OrganizationDto> CreateAsync(TenantId tenantId, long createdBy, OrganizationCreateRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task UpdateAsync(TenantId tenantId, long id, long updatedBy, OrganizationUpdateRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken) => throw new NotImplementedException();
    }

    private sealed class RecordingMemberService : IOrganizationMemberService
    {
        public List<(long OrgId, OrganizationMemberAddRequest Request)> AddCalls { get; } = new();

        public Task AddAsync(TenantId tenantId, long organizationId, long actorUserId, OrganizationMemberAddRequest request, CancellationToken cancellationToken)
        {
            AddCalls.Add((organizationId, request));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<OrganizationMemberDto>> ListAsync(TenantId tenantId, long organizationId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task UpdateAsync(TenantId tenantId, long organizationId, long targetUserId, OrganizationMemberUpdateRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task RemoveAsync(TenantId tenantId, long organizationId, long targetUserId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task MoveWorkspaceAsync(TenantId tenantId, long workspaceId, long targetOrganizationId, long actorUserId, CancellationToken cancellationToken) => throw new NotImplementedException();
    }

    private sealed class RecordingAuditWriter : IAuditWriter
    {
        public List<AuditRecord> Records { get; } = new();
        public Task WriteAsync(AuditRecord record, CancellationToken cancellationToken)
        {
            Records.Add(record);
            return Task.CompletedTask;
        }
    }
}
