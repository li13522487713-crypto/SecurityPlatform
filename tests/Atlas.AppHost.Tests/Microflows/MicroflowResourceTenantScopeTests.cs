using Atlas.Application.Microflows.Abstractions;
using Atlas.Application.Microflows.Audit;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Exceptions;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Repositories;
using Atlas.Application.Microflows.Runtime;
using Atlas.Application.Microflows.Services;
using Atlas.Domain.Microflows.Entities;
using NSubstitute;

namespace Atlas.AppHost.Tests.Microflows;

/// <summary>
/// P0-8 验证：repository EnsureScoped + 列表强制 TenantId/WorkspaceId。
/// </summary>
public sealed class MicroflowResourceTenantScopeTests
{
    [Fact]
    public async Task ListAsync_WithoutWorkspaceContext_ThrowsForbidden()
    {
        var (service, _, _) = Build(workspace: null, tenant: "tenant");

        var ex = await Assert.ThrowsAsync<MicroflowApiException>(() =>
            service.ListAsync(new ListMicroflowsRequestDto(), CancellationToken.None));
        Assert.Equal(MicroflowApiErrorCode.MicroflowWorkspaceForbidden, ex.Code);
        Assert.Equal(403, ex.HttpStatus);
    }

    [Fact]
    public async Task ListAsync_WithoutTenantContext_ThrowsForbidden()
    {
        var (service, _, _) = Build(workspace: "ws", tenant: null);

        var ex = await Assert.ThrowsAsync<MicroflowApiException>(() =>
            service.ListAsync(new ListMicroflowsRequestDto { WorkspaceId = "ws" }, CancellationToken.None));
        Assert.Equal(MicroflowApiErrorCode.MicroflowWorkspaceForbidden, ex.Code);
        Assert.Equal(403, ex.HttpStatus);
    }

    [Fact]
    public async Task GetAsync_WhenResourceBelongsToOtherWorkspace_Throws404()
    {
        var (service, repo, _) = Build(workspace: "ws-1", tenant: "tenant-1");
        repo.GetByIdAsync("victim", Arg.Any<CancellationToken>())
            .Returns(new MicroflowResourceEntity
            {
                Id = "victim",
                Name = "Victim",
                WorkspaceId = "ws-2",
                TenantId = "tenant-1",
            });

        var ex = await Assert.ThrowsAsync<MicroflowApiException>(() => service.GetAsync("victim", CancellationToken.None));
        Assert.Equal(MicroflowApiErrorCode.MicroflowNotFound, ex.Code);
        Assert.Equal(404, ex.HttpStatus);
    }

    [Fact]
    public async Task GetAsync_WhenResourceBelongsToOtherTenant_Throws404()
    {
        var (service, repo, _) = Build(workspace: "ws-1", tenant: "tenant-1");
        repo.GetByIdAsync("victim", Arg.Any<CancellationToken>())
            .Returns(new MicroflowResourceEntity
            {
                Id = "victim",
                Name = "Victim",
                WorkspaceId = "ws-1",
                TenantId = "tenant-2",
            });

        var ex = await Assert.ThrowsAsync<MicroflowApiException>(() => service.GetAsync("victim", CancellationToken.None));
        Assert.Equal(404, ex.HttpStatus);
    }

    private static (
        MicroflowResourceService Service,
        IMicroflowResourceRepository ResourceRepo,
        StubAccessor Accessor) Build(string? workspace, string? tenant)
    {
        var resourceRepo = Substitute.For<IMicroflowResourceRepository>();
        var folderRepo = Substitute.For<IMicroflowFolderRepository>();
        var snapshotRepo = Substitute.For<IMicroflowSchemaSnapshotRepository>();
        var refRepo = Substitute.For<IMicroflowReferenceRepository>();
        var indexer = Substitute.For<IMicroflowReferenceIndexer>();
        var auditWriter = Substitute.For<IMicroflowAuditWriter>();
        var accessor = new StubAccessor(workspace, tenant);
        var clock = new FixedClock();
        var service = new MicroflowResourceService(
            resourceRepo,
            folderRepo,
            snapshotRepo,
            refRepo,
            indexer,
            accessor,
            auditWriter,
            clock);
        return (service, resourceRepo, accessor);
    }

    private sealed class StubAccessor : IMicroflowRequestContextAccessor
    {
        public StubAccessor(string? workspace, string? tenant)
        {
            Current = new MicroflowRequestContext
            {
                WorkspaceId = workspace,
                TenantId = tenant,
                UserId = "user",
                TraceId = "trace",
            };
        }

        public MicroflowRequestContext Current { get; }
    }

    private sealed class FixedClock : IMicroflowClock
    {
        public DateTimeOffset UtcNow => new(2026, 4, 28, 0, 0, 0, TimeSpan.Zero);
    }
}
