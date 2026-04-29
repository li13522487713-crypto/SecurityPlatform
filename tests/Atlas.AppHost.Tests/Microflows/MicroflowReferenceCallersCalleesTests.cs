using Atlas.Application.Microflows.Abstractions;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Exceptions;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Repositories;
using Atlas.Application.Microflows.Services;
using Atlas.Domain.Microflows.Entities;
using NSubstitute;

namespace Atlas.AppHost.Tests.Microflows;

/// <summary>
/// P0-7 验证：callers / callees API 的服务层语义。
/// </summary>
public sealed class MicroflowReferenceCallersCalleesTests
{
    [Fact]
    public async Task ListCallers_ReturnsTargetSideReferences_AsActiveOnlyByDefault()
    {
        var (service, refRepo, resourceRepo) = Build();
        resourceRepo.GetByIdAsync("mf-target", Arg.Any<CancellationToken>())
            .Returns(new MicroflowResourceEntity { Id = "mf-target", Name = "Target", WorkspaceId = "ws", TenantId = "tenant" });
        refRepo.ListByTargetMicroflowIdAsync("mf-target", Arg.Any<MicroflowReferenceQuery>(), Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new MicroflowReferenceEntity { Id = "r1", TargetMicroflowId = "mf-target", SourceType = "microflow", SourceId = "mf-caller", Active = true }
            });

        var result = await service.ListCallersAsync("mf-target", new GetMicroflowReferencesRequestDto(), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("mf-caller", result[0].SourceId);
        Assert.True(result[0].CanNavigate);
    }

    [Fact]
    public async Task ListCallees_FiltersBySourceMicroflow()
    {
        var (service, refRepo, resourceRepo) = Build();
        resourceRepo.GetByIdAsync("mf-source", Arg.Any<CancellationToken>())
            .Returns(new MicroflowResourceEntity { Id = "mf-source", Name = "Source", WorkspaceId = "ws", TenantId = "tenant" });
        refRepo.ListBySourceAsync("microflow", "mf-source", Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new MicroflowReferenceEntity { Id = "r1", TargetMicroflowId = "mf-callee-1", SourceType = "microflow", SourceId = "mf-source", Active = true, ImpactLevel = "high" },
                new MicroflowReferenceEntity { Id = "r2", TargetMicroflowId = "mf-callee-2", SourceType = "microflow", SourceId = "mf-source", Active = false, ImpactLevel = "low" },
            });

        var result = await service.ListCalleesAsync(
            "mf-source",
            new GetMicroflowReferencesRequestDto { IncludeInactive = false },
            CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("mf-callee-1", result[0].TargetMicroflowId);
    }

    [Fact]
    public async Task ListCallees_HonoursImpactLevelFilter()
    {
        var (service, refRepo, resourceRepo) = Build();
        resourceRepo.GetByIdAsync("mf-source", Arg.Any<CancellationToken>())
            .Returns(new MicroflowResourceEntity { Id = "mf-source", Name = "Source" });
        refRepo.ListBySourceAsync("microflow", "mf-source", Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new MicroflowReferenceEntity { Id = "r1", TargetMicroflowId = "mf-x", SourceType = "microflow", SourceId = "mf-source", Active = true, ImpactLevel = "high" },
                new MicroflowReferenceEntity { Id = "r2", TargetMicroflowId = "mf-y", SourceType = "microflow", SourceId = "mf-source", Active = true, ImpactLevel = "low" },
            });

        var result = await service.ListCalleesAsync(
            "mf-source",
            new GetMicroflowReferencesRequestDto { ImpactLevel = new[] { "high" } },
            CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("mf-x", result[0].TargetMicroflowId);
    }

    [Fact]
    public async Task ListCallees_WhenResourceMissing_Throws404()
    {
        var (service, _, resourceRepo) = Build();
        resourceRepo.GetByIdAsync("missing", Arg.Any<CancellationToken>())
            .Returns((MicroflowResourceEntity?)null);

        var ex = await Assert.ThrowsAsync<MicroflowApiException>(() => service.ListCalleesAsync("missing", new GetMicroflowReferencesRequestDto(), CancellationToken.None));
        Assert.Equal(MicroflowApiErrorCode.MicroflowNotFound, ex.Code);
        Assert.Equal(404, ex.HttpStatus);
    }

    private static (
        MicroflowReferenceService Service,
        IMicroflowReferenceRepository RefRepo,
        IMicroflowResourceRepository ResourceRepo) Build()
    {
        var refRepo = Substitute.For<IMicroflowReferenceRepository>();
        var resourceRepo = Substitute.For<IMicroflowResourceRepository>();
        var indexer = Substitute.For<IMicroflowReferenceIndexer>();
        var accessor = new StubAccessor();
        var service = new MicroflowReferenceService(resourceRepo, refRepo, indexer, accessor);
        return (service, refRepo, resourceRepo);
    }

    private sealed class StubAccessor : IMicroflowRequestContextAccessor
    {
        public MicroflowRequestContext Current { get; } = new()
        {
            WorkspaceId = "ws",
            TenantId = "tenant",
            UserId = "user-1",
            TraceId = Guid.NewGuid().ToString("N")
        };
    }
}
