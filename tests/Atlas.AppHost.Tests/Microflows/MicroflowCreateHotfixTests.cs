using Atlas.Application.Microflows.Abstractions;
using Atlas.Application.Microflows.Audit;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Exceptions;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Repositories;
using Atlas.Application.Microflows.Services;
using Atlas.AppHost.Microflows.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Atlas.AppHost.Tests.Microflows;

public sealed class MicroflowCreateHotfixTests
{
    [Fact]
    public async Task CreateAsync_Succeeds_WithDisplayNameFallback()
    {
        var resourceRepository = Substitute.For<IMicroflowResourceRepository>();
        var folderRepository = Substitute.For<IMicroflowFolderRepository>();
        var snapshotRepository = Substitute.For<IMicroflowSchemaSnapshotRepository>();
        var referenceRepository = Substitute.For<IMicroflowReferenceRepository>();
        var referenceIndexer = Substitute.For<IMicroflowReferenceIndexer>();
        var contextAccessor = Substitute.For<IMicroflowRequestContextAccessor>();
        var clock = Substitute.For<IMicroflowClock>();
        var now = new DateTimeOffset(2026, 4, 28, 13, 0, 0, TimeSpan.Zero);
        clock.UtcNow.Returns(now);
        contextAccessor.Current.Returns(new MicroflowRequestContext
        {
            WorkspaceId = "workspace-1",
            TenantId = "tenant-1",
            UserId = "user-1",
            UserName = "Tester",
            TraceId = "trace-hotfix-create"
        });
        resourceRepository.ExistsByNameAsync("workspace-1", "OrderCreate", Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var service = new MicroflowResourceService(
            resourceRepository,
            folderRepository,
            snapshotRepository,
            referenceRepository,
            referenceIndexer,
            contextAccessor,
            new NullMicroflowAuditWriter(),
            clock);

        var result = await service.CreateAsync(new CreateMicroflowRequestDto
        {
            WorkspaceId = "workspace-1",
            Input = new MicroflowCreateInputDto
            {
                Name = "OrderCreate",
                ModuleId = "sales",
                ModuleName = "Sales",
                Tags = ["tag"],
            }
        }, CancellationToken.None);

        Assert.Equal("OrderCreate", result.Name);
        Assert.Equal("OrderCreate", result.DisplayName);
        Assert.Equal("sales", result.ModuleId);
        await resourceRepository.Received(1).InsertAsync(Arg.Any<Atlas.Domain.Microflows.Entities.MicroflowResourceEntity>(), Arg.Any<CancellationToken>());
        await snapshotRepository.Received(1).InsertAsync(Arg.Any<Atlas.Domain.Microflows.Entities.MicroflowSchemaSnapshotEntity>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_WithInvalidName_Throws422WithFieldError()
    {
        var service = CreateServiceForValidation();

        var ex = await Assert.ThrowsAsync<MicroflowApiException>(() => service.CreateAsync(new CreateMicroflowRequestDto
        {
            Input = new MicroflowCreateInputDto
            {
                Name = "_abc",
                ModuleId = "sales",
            }
        }, CancellationToken.None));

        Assert.Equal(422, ex.HttpStatus);
        Assert.Equal(MicroflowApiErrorCode.MicroflowValidationFailed, ex.Code);
        Assert.Contains(ex.FieldErrors, item => item.FieldPath == "input.name");
    }

    [Fact]
    public async Task CreateAsync_WithMissingModuleId_Throws422WithFieldError()
    {
        var service = CreateServiceForValidation();

        var ex = await Assert.ThrowsAsync<MicroflowApiException>(() => service.CreateAsync(new CreateMicroflowRequestDto
        {
            Input = new MicroflowCreateInputDto
            {
                Name = "OrderCreate",
                ModuleId = "",
            }
        }, CancellationToken.None));

        Assert.Equal(422, ex.HttpStatus);
        Assert.Equal(MicroflowApiErrorCode.MicroflowValidationFailed, ex.Code);
        Assert.Contains(ex.FieldErrors, item => item.FieldPath == "input.moduleId");
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateName_Throws409()
    {
        var resourceRepository = Substitute.For<IMicroflowResourceRepository>();
        var folderRepository = Substitute.For<IMicroflowFolderRepository>();
        var snapshotRepository = Substitute.For<IMicroflowSchemaSnapshotRepository>();
        var referenceRepository = Substitute.For<IMicroflowReferenceRepository>();
        var referenceIndexer = Substitute.For<IMicroflowReferenceIndexer>();
        var contextAccessor = Substitute.For<IMicroflowRequestContextAccessor>();
        var clock = Substitute.For<IMicroflowClock>();
        contextAccessor.Current.Returns(new MicroflowRequestContext { WorkspaceId = "workspace-1", TraceId = "trace-hotfix-dup" });
        resourceRepository.ExistsByNameAsync("workspace-1", "OrderCreate", Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(true);
        var service = new MicroflowResourceService(
            resourceRepository,
            folderRepository,
            snapshotRepository,
            referenceRepository,
            referenceIndexer,
            contextAccessor,
            new NullMicroflowAuditWriter(),
            clock);

        var ex = await Assert.ThrowsAsync<MicroflowApiException>(() => service.CreateAsync(new CreateMicroflowRequestDto
        {
            WorkspaceId = "workspace-1",
            Input = new MicroflowCreateInputDto
            {
                Name = "OrderCreate",
                ModuleId = "sales",
            }
        }, CancellationToken.None));

        Assert.Equal(409, ex.HttpStatus);
        Assert.Equal(MicroflowApiErrorCode.MicroflowNameDuplicated, ex.Code);
    }

    [Fact]
    public async Task ExceptionFilter_ReturnsEnvelopeAndTraceHeader()
    {
        var requestContextAccessor = Substitute.For<IMicroflowRequestContextAccessor>();
        requestContextAccessor.Current.Returns(new MicroflowRequestContext { TraceId = "trace-filter-1" });
        var filter = new MicroflowApiExceptionFilter(requestContextAccessor, NullLogger<MicroflowApiExceptionFilter>.Instance);
        var httpContext = new DefaultHttpContext();
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var exceptionContext = new ExceptionContext(actionContext, [])
        {
            Exception = new MicroflowApiException(
                MicroflowApiErrorCode.MicroflowValidationFailed,
                "校验失败",
                422,
                fieldErrors: [new MicroflowApiFieldError { FieldPath = "input.name", Code = "INVALID_FORMAT", Message = "name 格式非法。" }])
        };

        await filter.OnExceptionAsync(exceptionContext);

        Assert.True(exceptionContext.ExceptionHandled);
        var result = Assert.IsType<ObjectResult>(exceptionContext.Result);
        Assert.Equal(422, result.StatusCode);
        var envelope = Assert.IsType<MicroflowApiResponse<object>>(result.Value);
        Assert.False(envelope.Success);
        Assert.Equal("trace-filter-1", envelope.TraceId);
        Assert.Equal("trace-filter-1", envelope.Error?.TraceId);
        Assert.Equal("trace-filter-1", httpContext.Response.Headers["X-Trace-Id"].ToString());
    }

    private static MicroflowResourceService CreateServiceForValidation()
    {
        var resourceRepository = Substitute.For<IMicroflowResourceRepository>();
        var folderRepository = Substitute.For<IMicroflowFolderRepository>();
        var snapshotRepository = Substitute.For<IMicroflowSchemaSnapshotRepository>();
        var referenceRepository = Substitute.For<IMicroflowReferenceRepository>();
        var referenceIndexer = Substitute.For<IMicroflowReferenceIndexer>();
        var contextAccessor = Substitute.For<IMicroflowRequestContextAccessor>();
        var clock = Substitute.For<IMicroflowClock>();
        contextAccessor.Current.Returns(new MicroflowRequestContext { WorkspaceId = "workspace-1", TraceId = "trace-hotfix-validation" });
        resourceRepository.ExistsByNameAsync(Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(false);
        return new MicroflowResourceService(
            resourceRepository,
            folderRepository,
            snapshotRepository,
            referenceRepository,
            referenceIndexer,
            contextAccessor,
            new NullMicroflowAuditWriter(),
            clock);
    }
}
