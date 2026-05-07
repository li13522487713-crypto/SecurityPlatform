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

    [Fact]
    public void Get_exposes_state_available_commands_and_last_updated_at()
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
            Status = MicroflowDebugSessionLifecycle.Paused,
            CurrentSafePoint = new MicroflowDebugSafePointSnapshot
            {
                NodeObjectId = "node-1",
                NodeKind = "actionActivity",
                Phase = "beforeNode",
                CallDepth = 0,
                SemanticKind = "node",
                ArrivedAt = DateTimeOffset.UtcNow
            }
        });
        var controller = CreateController(store, accessor);

        var result = controller.Get(session.Id);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var envelope = Assert.IsType<MicroflowApiResponse<MicroflowDebugSession?>>(ok.Value);
        Assert.Equal(MicroflowDebugSessionLifecycle.Paused, envelope.Data!.State);
        Assert.NotEmpty(envelope.Data.AvailableCommands);
        Assert.Contains(DebugCommandKind.Continue, envelope.Data.AvailableCommands);
        Assert.True(envelope.Data.LastUpdatedAt > envelope.Data.CreatedAt);
    }

    [Fact]
    public void SetSuspendPolicy_rejects_invalid_policy()
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

        var result = controller.SetSuspendPolicy(session.Id, new MicroflowDebugController.DebugSuspendPolicyRequest
        {
            Policy = "invalid-policy"
        });

        var obj = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(422, obj.StatusCode);
    }

    [Fact]
    public void SetSuspendPolicy_accepts_branch_only()
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

        var result = controller.SetSuspendPolicy(session.Id, new MicroflowDebugController.DebugSuspendPolicyRequest
        {
            Policy = "branchOnly"
        });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var envelope = Assert.IsType<MicroflowApiResponse<MicroflowDebugController.DebugSuspendPolicyResult>>(ok.Value);
        Assert.Equal(session.Id, envelope.Data!.SessionId);
        Assert.Equal("branchOnly", envelope.Data.Policy);
    }

    [Fact]
    public void Timeline_returns_items_ordered_descending_by_created_at()
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
        var older = DateTimeOffset.UtcNow.AddMinutes(-2);
        var newer = DateTimeOffset.UtcNow.AddMinutes(-1);
        store.Upsert(session with
        {
            Trace =
            [
                new DebugTraceEvent { Id = "evt-older", Kind = "beforeNode", Message = "older", CreatedAt = older, RunId = "run-1", NodeObjectId = "node-1" },
                new DebugTraceEvent { Id = "evt-newer", Kind = "afterNode", Message = "newer", CreatedAt = newer, RunId = "run-1", NodeObjectId = "node-2" }
            ]
        });
        var controller = CreateController(store, accessor);

        var result = controller.Timeline(session.Id, take: 10);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var envelope = Assert.IsType<MicroflowApiResponse<IReadOnlyList<MicroflowDebugController.DebugTimelineItem>>>(ok.Value);
        Assert.Equal(2, envelope.Data!.Count);
        Assert.Equal("evt-newer", envelope.Data[0].Id);
        Assert.Equal("evt-older", envelope.Data[1].Id);
    }

    [Fact]
    public void MutateVariable_requires_pause_point()
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
                    Name = "flag",
                    Type = "boolean",
                    ValuePreview = "false"
                }
            ]
        });
        var controller = CreateController(store, accessor);

        var result = controller.MutateVariable(session.Id, new MicroflowDebugController.DebugVariableMutateRequest
        {
            Name = "flag",
            ValuePreview = "true",
            AllowUnsafe = true
        });

        var obj = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(409, obj.StatusCode);
    }

    [Fact]
    public void MutateVariable_updates_snapshot_when_paused()
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
            CurrentSafePoint = new MicroflowDebugSafePointSnapshot
            {
                NodeObjectId = "node-1",
                NodeKind = "actionActivity",
                Phase = "beforeNode",
                CallDepth = 0,
                SemanticKind = "node",
                ArrivedAt = DateTimeOffset.UtcNow
            },
            Variables =
            [
                new DebugVariableSnapshot
                {
                    Name = "flag",
                    Type = "boolean",
                    ValuePreview = "false"
                }
            ]
        });
        var controller = CreateController(store, accessor);

        var result = controller.MutateVariable(session.Id, new MicroflowDebugController.DebugVariableMutateRequest
        {
            Name = "flag",
            ValuePreview = "true",
            RawValueJson = "true",
            AllowUnsafe = true
        });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var envelope = Assert.IsType<MicroflowApiResponse<MicroflowDebugController.DebugVariableMutationResult>>(ok.Value);
        Assert.True(envelope.Data!.Mutated);
        Assert.Equal("flag", envelope.Data.Name);
        Assert.Equal("true", envelope.Data.ValuePreview);
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
