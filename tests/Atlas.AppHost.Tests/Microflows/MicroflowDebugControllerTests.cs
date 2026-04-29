using Atlas.AppHost.Microflows.Controllers;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Runtime.Debug;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace Atlas.AppHost.Tests.Microflows;

public sealed class MicroflowDebugControllerTests
{
    [Fact]
    public void Create_returns_429_when_debug_session_cap_reached()
    {
        var store = new InMemoryDebugSessionStore();
        var accessor = Substitute.For<IMicroflowRequestContextAccessor>();
        accessor.Current.Returns(new MicroflowRequestContext
        {
            WorkspaceId = "workspace-1",
            TenantId = "tenant-1",
            TraceId = "trace-debug"
        });
        var coordinator = new MicroflowDebugCoordinator(store);
        var controller = new MicroflowDebugController(store, coordinator, accessor)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        for (var i = 0; i < 128; i++)
            store.Create("microflow-any");

        var result = controller.Create("microflow-any");
        var obj = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(429, obj.StatusCode);
    }

    [Fact]
    public void Get_returns_403_for_cross_workspace_debug_session()
    {
        var store = new InMemoryDebugSessionStore();
        var accessor = Substitute.For<IMicroflowRequestContextAccessor>();
        accessor.Current.Returns(new MicroflowRequestContext
        {
            WorkspaceId = "workspace-2",
            TenantId = "tenant-1",
            UserId = "user-1",
            TraceId = "trace-debug"
        });
        var session = store.Create("microflow-any", new MicroflowDebugSessionOwner
        {
            WorkspaceId = "workspace-1",
            TenantId = "tenant-1",
            UserId = "user-1"
        });
        var controller = CreateController(store, accessor);

        var result = controller.Get(session.Id);

        var obj = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(403, obj.StatusCode);
    }

    [Fact]
    public void Variables_returns_current_paused_snapshot_with_redaction()
    {
        var store = new InMemoryDebugSessionStore();
        var accessor = Substitute.For<IMicroflowRequestContextAccessor>();
        accessor.Current.Returns(new MicroflowRequestContext
        {
            WorkspaceId = "workspace-1",
            TenantId = "tenant-1",
            UserId = "user-1",
            TraceId = "trace-debug"
        });
        var session = store.Create("microflow-any", new MicroflowDebugSessionOwner
        {
            WorkspaceId = "workspace-1",
            TenantId = "tenant-1",
            UserId = "user-1"
        });
        store.Upsert(session with
        {
            Variables =
            [
                new DebugVariableSnapshot
                {
                    Name = "apiToken",
                    Type = "string",
                    ValuePreview = "***",
                    RedactionApplied = true
                }
            ]
        });
        var controller = CreateController(store, accessor);

        var result = controller.Variables(session.Id);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var envelope = Assert.IsType<MicroflowApiResponse<IReadOnlyList<DebugVariableSnapshot>>>(ok.Value);
        var variable = Assert.Single(envelope.Data!);
        Assert.True(variable.RedactionApplied);
        Assert.Equal("***", variable.ValuePreview);
    }

    [Fact]
    public void Evaluate_reads_current_snapshot_variable_without_mutating_runtime()
    {
        var store = new InMemoryDebugSessionStore();
        var accessor = Substitute.For<IMicroflowRequestContextAccessor>();
        accessor.Current.Returns(new MicroflowRequestContext
        {
            WorkspaceId = "workspace-1",
            TenantId = "tenant-1",
            UserId = "user-1",
            TraceId = "trace-debug"
        });
        var session = store.Create("microflow-any", new MicroflowDebugSessionOwner
        {
            WorkspaceId = "workspace-1",
            TenantId = "tenant-1",
            UserId = "user-1"
        });
        store.Upsert(session with
        {
            Variables =
            [
                new DebugVariableSnapshot
                {
                    Name = "amount",
                    Type = "integer",
                    ValuePreview = "42"
                }
            ]
        });
        var controller = CreateController(store, accessor);

        var result = controller.Evaluate(session.Id, new DebugWatchExpression { Expression = "$amount" });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var envelope = Assert.IsType<MicroflowApiResponse<DebugWatchExpression>>(ok.Value);
        Assert.Equal("integer", envelope.Data!.Type);
        Assert.Equal("42", envelope.Data.ValuePreview);
        Assert.Null(envelope.Data.Error);
    }

    [Fact]
    public void Evaluate_reads_member_path_from_current_snapshot_json()
    {
        var store = new InMemoryDebugSessionStore();
        var accessor = Substitute.For<IMicroflowRequestContextAccessor>();
        accessor.Current.Returns(new MicroflowRequestContext
        {
            WorkspaceId = "workspace-1",
            TenantId = "tenant-1",
            UserId = "user-1",
            TraceId = "trace-debug"
        });
        var session = store.Create("microflow-any", new MicroflowDebugSessionOwner
        {
            WorkspaceId = "workspace-1",
            TenantId = "tenant-1",
            UserId = "user-1"
        });
        store.Upsert(session with
        {
            Variables =
            [
                new DebugVariableSnapshot
                {
                    Name = "order",
                    Type = "object",
                    ValuePreview = "{...}",
                    RawValueJson = """{"amount":42,"customer":{"name":"Ada"}}"""
                }
            ]
        });
        var controller = CreateController(store, accessor);

        var result = controller.Evaluate(session.Id, new DebugWatchExpression { Expression = "$order.customer.name" });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var envelope = Assert.IsType<MicroflowApiResponse<DebugWatchExpression>>(ok.Value);
        Assert.Equal("string", envelope.Data!.Type);
        Assert.Equal("Ada", envelope.Data.ValuePreview);
        Assert.Null(envelope.Data.Error);
    }

    [Fact]
    public void Evaluate_reports_error_without_changing_session_when_watch_is_invalid()
    {
        var store = new InMemoryDebugSessionStore();
        var accessor = Substitute.For<IMicroflowRequestContextAccessor>();
        accessor.Current.Returns(new MicroflowRequestContext
        {
            WorkspaceId = "workspace-1",
            TenantId = "tenant-1",
            UserId = "user-1",
            TraceId = "trace-debug"
        });
        var session = store.Create("microflow-any", new MicroflowDebugSessionOwner
        {
            WorkspaceId = "workspace-1",
            TenantId = "tenant-1",
            UserId = "user-1"
        });
        var controller = CreateController(store, accessor);

        var result = controller.Evaluate(session.Id, new DebugWatchExpression { Expression = "$missing" });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var envelope = Assert.IsType<MicroflowApiResponse<DebugWatchExpression>>(ok.Value);
        Assert.NotNull(envelope.Data!.Error);
        Assert.Equal("created", store.Get(session.Id)?.Status);
    }

    private static MicroflowDebugController CreateController(InMemoryDebugSessionStore store, IMicroflowRequestContextAccessor accessor)
    {
        var coordinator = new MicroflowDebugCoordinator(store);
        return new MicroflowDebugController(store, coordinator, accessor)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }
}
