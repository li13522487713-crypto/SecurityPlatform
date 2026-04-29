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
}
