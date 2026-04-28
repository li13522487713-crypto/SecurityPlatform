using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Atlas.Application.Authorization;
using Atlas.Application.Identity.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Identity;
using Atlas.Core.Tenancy;
using Atlas.Infrastructure.Services.Authorization;
using Xunit;

namespace Atlas.SecurityPlatform.Tests.Authorization;

/// <summary>
/// 覆盖 M-G03-C3..C5（S6）：ResourceWriteGate 行为。
/// </summary>
public sealed class ResourceWriteGateTests
{
    private static readonly TenantId Tenant = new(Guid.Parse("00000000-0000-0000-0000-000000000020"));

    [Fact]
    public async Task GuardAsync_ShouldShortCircuit_WhenNoUserContext()
    {
        var guard = new RecordingGuard();
        var pdp = new RecordingPdp();
        var current = new NullCurrentUserAccessor();
        var lookup = new RecordingWorkspaceLookup();
        var gate = new ResourceWriteGate(guard, pdp, current, lookup);

        await gate.GuardAsync(Tenant, 100, "agent", 1, "edit", CancellationToken.None);

        Assert.Single(guard.Calls);
        Assert.True(guard.Calls[0].IsPlatformAdmin); // 短路：按平台 admin 通过
    }

    [Fact]
    public async Task GuardAsync_ShouldUseUserContext_WhenPresent()
    {
        var guard = new RecordingGuard();
        var pdp = new RecordingPdp();
        var current = new FixedCurrentUserAccessor(new CurrentUserInfo(
            UserId: 9527,
            Username: "u",
            DisplayName: "u",
            TenantId: Tenant,
            Roles: Array.Empty<string>(),
            IsPlatformAdmin: false));
        var lookup = new RecordingWorkspaceLookup();
        var gate = new ResourceWriteGate(guard, pdp, current, lookup);

        await gate.GuardAsync(Tenant, 100, "agent", 1, "edit", CancellationToken.None);

        Assert.Single(guard.Calls);
        Assert.Equal(9527L, guard.Calls[0].UserId);
        Assert.False(guard.Calls[0].IsPlatformAdmin);
    }

    [Fact]
    public async Task GuardAsync_ShouldThrow_WhenGuardThrows()
    {
        var guard = new RecordingGuard { ShouldThrow = true };
        var pdp = new RecordingPdp();
        var current = new FixedCurrentUserAccessor(new CurrentUserInfo(7, "u", "u", Tenant, Array.Empty<string>()));
        var lookup = new RecordingWorkspaceLookup();
        var gate = new ResourceWriteGate(guard, pdp, current, lookup);

        await Assert.ThrowsAsync<BusinessException>(() =>
            gate.GuardAsync(Tenant, 100, "agent", 1, "delete", CancellationToken.None));
    }

    [Fact]
    public async Task InvalidateAsync_ShouldDelegateToPdp()
    {
        var guard = new RecordingGuard();
        var pdp = new RecordingPdp();
        var current = new NullCurrentUserAccessor();
        var lookup = new RecordingWorkspaceLookup();
        var gate = new ResourceWriteGate(guard, pdp, current, lookup);

        await gate.InvalidateAsync(Tenant, "agent", 9001, CancellationToken.None);

        Assert.Single(pdp.InvalidateCalls);
        Assert.Equal("agent", pdp.InvalidateCalls[0].ResourceType);
        Assert.Equal(9001L, pdp.InvalidateCalls[0].ResourceId);
    }

    [Fact]
    public async Task GuardByResourceAsync_ShouldShortCircuit_WhenWorkspaceMissing()
    {
        var guard = new RecordingGuard();
        var pdp = new RecordingPdp();
        var current = new FixedCurrentUserAccessor(new CurrentUserInfo(7, "u", "u", Tenant, Array.Empty<string>()));
        var lookup = new RecordingWorkspaceLookup(); // 默认返回 null
        var gate = new ResourceWriteGate(guard, pdp, current, lookup);

        await gate.GuardByResourceAsync(Tenant, "workflow", 9001, "edit", CancellationToken.None);

        // 资源没有 workspaceId → 不调 guard
        Assert.Empty(guard.Calls);
    }

    [Fact]
    public async Task GuardByResourceAsync_ShouldDelegateToGuard_WhenWorkspaceResolved()
    {
        var guard = new RecordingGuard();
        var pdp = new RecordingPdp();
        var current = new FixedCurrentUserAccessor(new CurrentUserInfo(7, "u", "u", Tenant, Array.Empty<string>()));
        var lookup = new RecordingWorkspaceLookup { ResolvedWorkspaceId = 200200 };
        var gate = new ResourceWriteGate(guard, pdp, current, lookup);

        await gate.GuardByResourceAsync(Tenant, "workflow", 9001, "edit", CancellationToken.None);

        Assert.Single(guard.Calls);
        Assert.Equal(200200L, guard.Calls[0].WorkspaceId);
        Assert.Equal("workflow", guard.Calls[0].ResourceType);
    }

    private sealed class RecordingGuard : IResourceAccessGuard
    {
        public bool ShouldThrow { get; set; }
        public List<ResourceAccessQuery> Calls { get; } = new();

        public Task<ResourceAccessDecision> CheckAsync(ResourceAccessQuery query, CancellationToken cancellationToken)
        {
            Calls.Add(query);
            return Task.FromResult(ShouldThrow
                ? new ResourceAccessDecision(false, "AccessDenied", null)
                : new ResourceAccessDecision(true, null, "platform"));
        }

        public Task RequireAsync(ResourceAccessQuery query, CancellationToken cancellationToken)
        {
            Calls.Add(query);
            if (ShouldThrow)
            {
                throw new BusinessException("FORBIDDEN", "AccessDenied");
            }
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingPdp : IPermissionDecisionService
    {
        public List<(TenantId T, string ResourceType, long ResourceId)> InvalidateCalls { get; } = new();
        public Task<bool> HasPermissionAsync(TenantId tenantId, long userId, string permissionCode, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<bool> IsSystemAdminAsync(TenantId tenantId, long userId, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task InvalidateUserAsync(TenantId tenantId, long userId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task InvalidateRoleAsync(TenantId tenantId, long roleId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task InvalidateTenantAsync(TenantId tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task InvalidateResourceAsync(TenantId tenantId, string resourceType, long resourceId, CancellationToken cancellationToken = default)
        {
            InvalidateCalls.Add((tenantId, resourceType, resourceId));
            return Task.CompletedTask;
        }
    }

    private sealed class NullCurrentUserAccessor : ICurrentUserAccessor
    {
        public CurrentUserInfo? GetCurrentUser() => null;
        public CurrentUserInfo GetCurrentUserOrThrow() => throw new InvalidOperationException("no user");
    }

    private sealed class FixedCurrentUserAccessor : ICurrentUserAccessor
    {
        private readonly CurrentUserInfo _user;
        public FixedCurrentUserAccessor(CurrentUserInfo user) => _user = user;
        public CurrentUserInfo? GetCurrentUser() => _user;
        public CurrentUserInfo GetCurrentUserOrThrow() => _user;
    }

    private sealed class RecordingWorkspaceLookup : IResourceWorkspaceLookup
    {
        public long? ResolvedWorkspaceId { get; set; }
        public List<(string Type, long Id)> Calls { get; } = new();

        public Task<long?> ResolveWorkspaceIdAsync(TenantId tenantId, string resourceType, long resourceId, CancellationToken cancellationToken)
        {
            Calls.Add((resourceType, resourceId));
            return Task.FromResult(ResolvedWorkspaceId);
        }
    }
}
