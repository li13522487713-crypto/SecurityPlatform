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

public sealed class MicroflowFolderServiceTests
{
    [Fact]
    public async Task CreateAsync_CalculatesNestedPath()
    {
        var fixture = CreateFixture();
        fixture.FolderRepository.GetByIdAsync("parent-1", Arg.Any<CancellationToken>())
            .Returns(new MicroflowFolderEntity
            {
                Id = "parent-1",
                WorkspaceId = "workspace-1",
                TenantId = "tenant-1",
                ModuleId = "sales",
                Name = "OrderProcessing",
                Path = "OrderProcessing",
                Depth = 1
            });
        MicroflowFolderEntity? inserted = null;
        fixture.FolderRepository.InsertAsync(Arg.Do<MicroflowFolderEntity>(entity => inserted = entity), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var result = await fixture.Service.CreateAsync(new CreateMicroflowFolderRequestDto
        {
            WorkspaceId = "workspace-1",
            ModuleId = "sales",
            ParentFolderId = "parent-1",
            Name = "Validation"
        }, CancellationToken.None);

        Assert.Equal("Validation", result.Name);
        Assert.Equal("OrderProcessing/Validation", result.Path);
        Assert.Equal(2, result.Depth);
        Assert.Equal("tenant-1", inserted?.TenantId);
        Assert.Equal("user-1", inserted?.CreatedBy);
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateSiblingName_Throws409()
    {
        var fixture = CreateFixture();
        fixture.FolderRepository.ExistsBySiblingNameAsync("workspace-1", "tenant-1", "sales", null, "Validation", null, Arg.Any<CancellationToken>())
            .Returns(true);

        var ex = await Assert.ThrowsAsync<MicroflowApiException>(() => fixture.Service.CreateAsync(new CreateMicroflowFolderRequestDto
        {
            WorkspaceId = "workspace-1",
            ModuleId = "sales",
            Name = "Validation"
        }, CancellationToken.None));

        Assert.Equal(409, ex.HttpStatus);
        Assert.Equal(MicroflowApiErrorCode.MicroflowFolderNameDuplicated, ex.Code);
    }

    [Fact]
    public async Task MoveAsync_ToDescendant_Throws409()
    {
        var fixture = CreateFixture();
        var root = Folder("root", "Root", "Root", 1);
        var child = Folder("child", "Child", "Root/Child", 2, "root");
        fixture.FolderRepository.GetByIdAsync("root", Arg.Any<CancellationToken>()).Returns(root);
        fixture.FolderRepository.GetByIdAsync("child", Arg.Any<CancellationToken>()).Returns(child);

        var ex = await Assert.ThrowsAsync<MicroflowApiException>(() => fixture.Service.MoveAsync("root", new MoveMicroflowFolderRequestDto
        {
            ParentFolderId = "child"
        }, CancellationToken.None));

        Assert.Equal(409, ex.HttpStatus);
        Assert.Equal(MicroflowApiErrorCode.MicroflowFolderCycle, ex.Code);
    }

    [Fact]
    public async Task DeleteAsync_WithResources_Throws409()
    {
        var fixture = CreateFixture();
        var folder = Folder("folder-1", "Validation", "Validation", 1);
        fixture.FolderRepository.GetByIdAsync("folder-1", Arg.Any<CancellationToken>()).Returns(folder);
        fixture.FolderRepository.ListByModuleAsync("workspace-1", "tenant-1", "sales", Arg.Any<CancellationToken>())
            .Returns([folder]);
        fixture.ResourceRepository.ListByFolderIdAsync("folder-1", Arg.Any<CancellationToken>())
            .Returns([new MicroflowResourceEntity { Id = "mf-1", ModuleId = "sales", Name = "MF_Check", DisplayName = "MF_Check" }]);

        var ex = await Assert.ThrowsAsync<MicroflowApiException>(() => fixture.Service.DeleteAsync("folder-1", CancellationToken.None));

        Assert.Equal(409, ex.HttpStatus);
        Assert.Equal(MicroflowApiErrorCode.MicroflowFolderNotEmpty, ex.Code);
        await fixture.FolderRepository.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private static MicroflowFolderEntity Folder(string id, string name, string path, int depth, string? parentFolderId = null)
    {
        return new MicroflowFolderEntity
        {
            Id = id,
            WorkspaceId = "workspace-1",
            TenantId = "tenant-1",
            ModuleId = "sales",
            ParentFolderId = parentFolderId,
            Name = name,
            Path = path,
            Depth = depth
        };
    }

    private static Fixture CreateFixture()
    {
        var folderRepository = Substitute.For<IMicroflowFolderRepository>();
        var resourceRepository = Substitute.For<IMicroflowResourceRepository>();
        var contextAccessor = Substitute.For<IMicroflowRequestContextAccessor>();
        var clock = Substitute.For<IMicroflowClock>();
        var transaction = Substitute.For<IMicroflowStorageTransaction>();
        contextAccessor.Current.Returns(new MicroflowRequestContext
        {
            WorkspaceId = "workspace-1",
            TenantId = "tenant-1",
            UserId = "user-1",
            TraceId = "trace-folder-test"
        });
        clock.UtcNow.Returns(new DateTimeOffset(2026, 4, 28, 17, 0, 0, TimeSpan.Zero));
        folderRepository.ExistsBySiblingNameAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(false);
        resourceRepository.ListByFolderIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([]);
        transaction.ExecuteAsync(Arg.Any<Func<Task>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<Task>>()());

        return new Fixture(
            new MicroflowFolderService(folderRepository, resourceRepository, contextAccessor, clock, transaction),
            folderRepository,
            resourceRepository);
    }

    private sealed record Fixture(
        MicroflowFolderService Service,
        IMicroflowFolderRepository FolderRepository,
        IMicroflowResourceRepository ResourceRepository);
}
