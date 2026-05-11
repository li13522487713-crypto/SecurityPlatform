using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Atlas.AppHost.Microflows.Controllers;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Runtime.Debug;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace Atlas.AppHost.Tests.Microflows;

public sealed class MicroflowDebugControllerTests
{
    [Fact]
    public async Task Connect_returns_400_when_request_is_not_websocket()
    {
        var store = new InMemoryDebugSessionStore();
        var accessor = CreateAccessor("workspace-1", "tenant-1", "user-1");
        var controller = CreateController(store, accessor, isWebSocketRequest: false);

        await controller.Connect("microflow-any", null, CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, controller.HttpContext.Response.StatusCode);
    }

    [Fact]
    public async Task Connect_returns_429_when_debug_session_cap_reached()
    {
        var store = new InMemoryDebugSessionStore();
        var accessor = CreateAccessor("workspace-1", "tenant-1", "user-1");
        var controller = CreateController(store, accessor, isWebSocketRequest: true);
        for (var i = 0; i < 256; i++)
        {
            store.Create("microflow-any");
        }

        await controller.Connect("microflow-any", null, CancellationToken.None);

        Assert.Equal(StatusCodes.Status429TooManyRequests, controller.HttpContext.Response.StatusCode);
    }

    [Fact]
    public async Task Connect_returns_403_when_reusing_cross_workspace_session()
    {
        var store = new InMemoryDebugSessionStore();
        var ownerAccessor = CreateAccessor("workspace-1", "tenant-1", "user-1");
        var ownerSession = store.Create("microflow-any", new MicroflowDebugSessionOwner
        {
            WorkspaceId = ownerAccessor.Current.WorkspaceId,
            TenantId = ownerAccessor.Current.TenantId,
            UserId = ownerAccessor.Current.UserId
        });
        var accessor = CreateAccessor("workspace-2", "tenant-1", "user-1");
        var controller = CreateController(store, accessor, isWebSocketRequest: true);

        await controller.Connect("microflow-any", ownerSession.Id, CancellationToken.None);

        Assert.Equal(StatusCodes.Status403Forbidden, controller.HttpContext.Response.StatusCode);
    }

    [Fact]
    public async Task Connect_websocket_flow_sends_session_status_state_sync_and_pong()
    {
        var store = new InMemoryDebugSessionStore();
        var accessor = CreateAccessor("workspace-1", "tenant-1", "user-1");
        var feature = new StubWebSocketFeature(isWebSocketRequest: true)
        {
            SocketFactory = () => new FakeWebSocket(
            [
                """{"type":"ping","data":{"sequence":123}}"""
            ])
        };
        var controller = CreateController(store, accessor, feature);

        await controller.Connect("microflow-any", "session-ws-1", CancellationToken.None);

        var socket = Assert.IsType<FakeWebSocket>(feature.AcceptedSocket);
        var payloadTypes = socket.SentPayloads
            .Select(payload =>
            {
                using var doc = JsonDocument.Parse(payload);
                return doc.RootElement.TryGetProperty("type", out var typeElement) ? typeElement.GetString() : null;
            })
            .Where(type => !string.IsNullOrWhiteSpace(type))
            .ToArray();

        Assert.Contains("session-status", payloadTypes);
        Assert.Contains("state-sync", payloadTypes);
        Assert.Contains("pong", payloadTypes);
    }

    private static MicroflowDebugController CreateController(
        InMemoryDebugSessionStore store,
        IMicroflowRequestContextAccessor accessor,
        bool isWebSocketRequest)
    {
        var coordinator = new MicroflowDebugCoordinator(store);
        var context = new DefaultHttpContext();
        context.Features.Set<IHttpWebSocketFeature>(new StubWebSocketFeature(isWebSocketRequest));

        return new MicroflowDebugController(store, coordinator, accessor)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = context
            }
        };
    }

    private static MicroflowDebugController CreateController(
        InMemoryDebugSessionStore store,
        IMicroflowRequestContextAccessor accessor,
        StubWebSocketFeature webSocketFeature)
    {
        var coordinator = new MicroflowDebugCoordinator(store);
        var context = new DefaultHttpContext();
        context.Features.Set<IHttpWebSocketFeature>(webSocketFeature);

        return new MicroflowDebugController(store, coordinator, accessor)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = context
            }
        };
    }

    private static IMicroflowRequestContextAccessor CreateAccessor(string workspaceId, string tenantId, string userId)
    {
        var accessor = Substitute.For<IMicroflowRequestContextAccessor>();
        accessor.Current.Returns(new MicroflowRequestContext
        {
            WorkspaceId = workspaceId,
            TenantId = tenantId,
            UserId = userId,
            TraceId = "trace-debug"
        });
        return accessor;
    }

    private sealed class StubWebSocketFeature(bool isWebSocketRequest) : IHttpWebSocketFeature
    {
        public bool IsWebSocketRequest { get; } = isWebSocketRequest;

        public Func<WebSocket>? SocketFactory { get; init; }

        public WebSocket? AcceptedSocket { get; private set; }

        public Task<WebSocket> AcceptAsync(WebSocketAcceptContext context)
        {
            AcceptedSocket = SocketFactory?.Invoke() ?? new FakeWebSocket([]);
            return Task.FromResult(AcceptedSocket);
        }
    }

    private sealed class FakeWebSocket(IReadOnlyList<string> incomingMessages) : WebSocket
    {
        private readonly Queue<string> _incomingMessages = new(incomingMessages);
        private WebSocketState _state = WebSocketState.Open;

        public IReadOnlyList<string> SentPayloads => _sentPayloads;
        private readonly List<string> _sentPayloads = [];

        public override WebSocketCloseStatus? CloseStatus => WebSocketCloseStatus.NormalClosure;

        public override string? CloseStatusDescription => "closed";

        public override WebSocketState State => _state;

        public override string? SubProtocol => null;

        public override void Abort()
        {
            _state = WebSocketState.Aborted;
        }

        public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
        {
            _state = WebSocketState.Closed;
            return Task.CompletedTask;
        }

        public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
        {
            _state = WebSocketState.CloseSent;
            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            _state = WebSocketState.Closed;
        }

        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            if (_incomingMessages.Count == 0)
            {
                _state = WebSocketState.CloseReceived;
                return Task.FromResult(new WebSocketReceiveResult(0, WebSocketMessageType.Close, true));
            }

            var payload = _incomingMessages.Dequeue();
            var bytes = Encoding.UTF8.GetBytes(payload);
            Array.Copy(bytes, 0, buffer.Array!, buffer.Offset, bytes.Length);
            return Task.FromResult(new WebSocketReceiveResult(bytes.Length, WebSocketMessageType.Text, true));
        }

        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            _sentPayloads.Add(Encoding.UTF8.GetString(buffer.Array!, buffer.Offset, buffer.Count));
            return Task.CompletedTask;
        }
    }
}
