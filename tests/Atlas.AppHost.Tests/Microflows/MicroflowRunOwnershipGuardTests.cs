using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Exceptions;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Repositories;
using Atlas.Application.Microflows.Runtime;
using Atlas.Domain.Microflows.Entities;
using NSubstitute;

namespace Atlas.AppHost.Tests.Microflows;

/// <summary>
/// P0-3：覆盖 IDOR 修复——仅凭 runId 不应能跨 workspace/tenant 读取或取消 trace/session/cancel。
/// </summary>
public sealed class MicroflowRunOwnershipGuardTests
{
    [Fact]
    public async Task EnsureRunOwned_WhenRunIdEmpty_Throws404()
    {
        var (guard, _, _, _) = CreateGuard(workspace: "ws-1");

        var ex = await Assert.ThrowsAsync<MicroflowApiException>(() => guard.EnsureRunOwnedAsync("", CancellationToken.None));
        Assert.Equal(MicroflowApiErrorCode.MicroflowNotFound, ex.Code);
        Assert.Equal(404, ex.HttpStatus);
    }

    [Fact]
    public async Task EnsureRunOwned_WhenSessionMissing_Throws404()
    {
        var (guard, runRepo, _, _) = CreateGuard(workspace: "ws-1");
        runRepo.GetSessionAsync("missing", Arg.Any<CancellationToken>()).Returns((MicroflowRunSessionEntity?)null);

        var ex = await Assert.ThrowsAsync<MicroflowApiException>(() => guard.EnsureRunOwnedAsync("missing", CancellationToken.None));
        Assert.Equal(MicroflowApiErrorCode.MicroflowNotFound, ex.Code);
    }

    [Fact]
    public async Task EnsureRunOwned_WhenRequestContextMissingWorkspace_RejectsWith404()
    {
        var (guard, runRepo, _, _) = CreateGuard(workspace: null);
        runRepo.GetSessionAsync("r-1", Arg.Any<CancellationToken>()).Returns(new MicroflowRunSessionEntity
        {
            Id = "r-1",
            ResourceId = "mf-1",
            WorkspaceId = "ws-1",
            TenantId = "tenant-1"
        });

        var ex = await Assert.ThrowsAsync<MicroflowApiException>(() => guard.EnsureRunOwnedAsync("r-1", CancellationToken.None));
        Assert.Equal(404, ex.HttpStatus);
    }

    [Fact]
    public async Task EnsureRunOwned_WhenWorkspaceMismatch_Throws404()
    {
        var (guard, runRepo, _, _) = CreateGuard(workspace: "ws-attacker", tenant: "tenant-1");
        runRepo.GetSessionAsync("r-1", Arg.Any<CancellationToken>()).Returns(new MicroflowRunSessionEntity
        {
            Id = "r-1",
            ResourceId = "mf-1",
            WorkspaceId = "ws-victim",
            TenantId = "tenant-1"
        });

        var ex = await Assert.ThrowsAsync<MicroflowApiException>(() => guard.EnsureRunOwnedAsync("r-1", CancellationToken.None));
        Assert.Equal(MicroflowApiErrorCode.MicroflowNotFound, ex.Code);
        Assert.Equal(404, ex.HttpStatus);
    }

    [Fact]
    public async Task EnsureRunOwned_WhenTenantMismatch_Throws404()
    {
        var (guard, runRepo, _, _) = CreateGuard(workspace: "ws-1", tenant: "tenant-attacker");
        runRepo.GetSessionAsync("r-1", Arg.Any<CancellationToken>()).Returns(new MicroflowRunSessionEntity
        {
            Id = "r-1",
            ResourceId = "mf-1",
            WorkspaceId = "ws-1",
            TenantId = "tenant-victim"
        });

        var ex = await Assert.ThrowsAsync<MicroflowApiException>(() => guard.EnsureRunOwnedAsync("r-1", CancellationToken.None));
        Assert.Equal(404, ex.HttpStatus);
    }

    [Fact]
    public async Task EnsureRunOwned_WhenWorkspaceMatches_ReturnsSession()
    {
        var (guard, runRepo, _, _) = CreateGuard(workspace: "ws-1", tenant: "tenant-1");
        var session = new MicroflowRunSessionEntity
        {
            Id = "r-1",
            ResourceId = "mf-1",
            WorkspaceId = "ws-1",
            TenantId = "tenant-1"
        };
        runRepo.GetSessionAsync("r-1", Arg.Any<CancellationToken>()).Returns(session);

        var result = await guard.EnsureRunOwnedAsync("r-1", CancellationToken.None);
        Assert.Same(session, result);
    }

    [Fact]
    public async Task EnsureRunOwned_WhenLegacySessionMissingWorkspace_FallsBackToResourceWorkspace()
    {
        var (guard, runRepo, resourceRepo, _) = CreateGuard(workspace: "ws-1", tenant: "tenant-1");
        runRepo.GetSessionAsync("r-1", Arg.Any<CancellationToken>()).Returns(new MicroflowRunSessionEntity
        {
            Id = "r-1",
            ResourceId = "mf-1",
            WorkspaceId = null,
            TenantId = null
        });
        resourceRepo.GetByIdAsync("mf-1", Arg.Any<CancellationToken>()).Returns(new MicroflowResourceEntity
        {
            Id = "mf-1",
            WorkspaceId = "ws-1",
            TenantId = "tenant-1"
        });

        var result = await guard.EnsureRunOwnedAsync("r-1", CancellationToken.None);
        Assert.Equal("r-1", result.Id);
    }

    [Fact]
    public async Task EnsureRunOwned_WhenLegacyResourceWorkspaceMismatches_Throws404()
    {
        var (guard, runRepo, resourceRepo, _) = CreateGuard(workspace: "ws-1", tenant: "tenant-1");
        runRepo.GetSessionAsync("r-1", Arg.Any<CancellationToken>()).Returns(new MicroflowRunSessionEntity
        {
            Id = "r-1",
            ResourceId = "mf-1",
            WorkspaceId = null,
            TenantId = null
        });
        resourceRepo.GetByIdAsync("mf-1", Arg.Any<CancellationToken>()).Returns(new MicroflowResourceEntity
        {
            Id = "mf-1",
            WorkspaceId = "ws-other",
            TenantId = "tenant-1"
        });

        var ex = await Assert.ThrowsAsync<MicroflowApiException>(() => guard.EnsureRunOwnedAsync("r-1", CancellationToken.None));
        Assert.Equal(404, ex.HttpStatus);
    }

    private static (
        MicroflowRunOwnershipGuard Guard,
        IMicroflowRunRepository RunRepo,
        IMicroflowResourceRepository ResourceRepo,
        StubAccessor Accessor) CreateGuard(string? workspace, string? tenant = null)
    {
        var runRepo = Substitute.For<IMicroflowRunRepository>();
        var resourceRepo = Substitute.For<IMicroflowResourceRepository>();
        var accessor = new StubAccessor(workspace, tenant);
        return (new MicroflowRunOwnershipGuard(runRepo, resourceRepo, accessor), runRepo, resourceRepo, accessor);
    }

    private sealed class StubAccessor : IMicroflowRequestContextAccessor
    {
        public StubAccessor(string? workspace, string? tenant)
        {
            Current = new MicroflowRequestContext
            {
                WorkspaceId = workspace,
                TenantId = tenant,
                TraceId = "trace-id"
            };
        }

        public MicroflowRequestContext Current { get; }
    }
}
