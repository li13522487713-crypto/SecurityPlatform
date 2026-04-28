using Atlas.Application.Microflows.Abstractions;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Models;
using Atlas.AppHost.Microflows.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace Atlas.AppHost.Tests.Microflows;

public sealed class MicroflowFoldersControllerTests
{
    [Fact]
    public async Task Create_ReturnsEnvelopeWithTraceId()
    {
        var folderService = Substitute.For<IMicroflowFolderService>();
        var contextAccessor = Substitute.For<IMicroflowRequestContextAccessor>();
        contextAccessor.Current.Returns(new MicroflowRequestContext
        {
            WorkspaceId = "workspace-1",
            TenantId = "tenant-1",
            UserId = "user-1",
            TraceId = "trace-folder-controller"
        });
        folderService.CreateAsync(Arg.Any<CreateMicroflowFolderRequestDto>(), Arg.Any<CancellationToken>())
            .Returns(new MicroflowFolderDto
            {
                Id = "folder-1",
                WorkspaceId = "workspace-1",
                ModuleId = "sales",
                Name = "Validation",
                Path = "Validation",
                Depth = 1
            });
        var controller = new MicroflowFoldersController(folderService, contextAccessor)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var response = await controller.Create(new CreateMicroflowFolderRequestDto
        {
            WorkspaceId = "workspace-1",
            ModuleId = "sales",
            Name = "Validation"
        }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var envelope = Assert.IsType<MicroflowApiResponse<MicroflowFolderDto>>(ok.Value);
        Assert.True(envelope.Success);
        Assert.Equal("trace-folder-controller", envelope.TraceId);
        Assert.Equal("Validation", envelope.Data?.Name);
        Assert.Equal("trace-folder-controller", controller.Response.Headers["X-Trace-Id"].ToString());
    }

    [Fact]
    public async Task Delete_ReturnsDeletedFolderId()
    {
        var folderService = Substitute.For<IMicroflowFolderService>();
        var contextAccessor = Substitute.For<IMicroflowRequestContextAccessor>();
        contextAccessor.Current.Returns(new MicroflowRequestContext { TraceId = "trace-folder-delete" });
        var controller = new MicroflowFoldersController(folderService, contextAccessor)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var response = await controller.Delete("folder-1", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var envelope = Assert.IsType<MicroflowApiResponse<DeleteMicroflowResponseDto>>(ok.Value);
        Assert.True(envelope.Success);
        Assert.Equal("folder-1", envelope.Data?.Id);
        await folderService.Received(1).DeleteAsync("folder-1", Arg.Any<CancellationToken>());
    }
}
