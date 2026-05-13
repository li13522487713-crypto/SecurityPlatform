using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Abstractions;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Runtime;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Microflows.Controllers;

[Route("api/v1/microflows")]
public sealed class MicroflowRuntimeEventsController : MicroflowApiControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IMicroflowRuntimeWsEventStream _eventStream;
    private readonly IMicroflowTestRunService _testRunService;

    public MicroflowRuntimeEventsController(
        IMicroflowRuntimeWsEventStream eventStream,
        IMicroflowTestRunService testRunService,
        IMicroflowRequestContextAccessor requestContextAccessor)
        : base(requestContextAccessor)
    {
        _eventStream = eventStream;
        _testRunService = testRunService;
    }

    [HttpGet("runs/{runId}/runtime/events/snapshot")]
    [ProducesResponseType(typeof(MicroflowApiResponse<MicroflowRuntimeOverlaySnapshot>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MicroflowApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MicroflowApiResponse<MicroflowRuntimeOverlaySnapshot>>> GetSnapshot(
        string runId,
        [FromQuery] long? lastSequence,
        CancellationToken cancellationToken)
    {
        await EnsureHydratedAsync(runId, cancellationToken);
        var snapshot = _eventStream.GetSnapshot(runId, lastSequence ?? 0);
        if (snapshot is null)
        {
            return NotFound(MicroflowApiResponse<object>.Fail(new MicroflowApiError
            {
                Code = "MICROFLOW_RUN_NOT_FOUND",
                Message = "运行会话不存在。",
                TraceId = TraceId
            }, TraceId));
        }

        return MicroflowOk(snapshot);
    }

    [HttpGet("runs/{runId}/runtime/events/ws")]
    public async Task Connect(
        string runId,
        [FromQuery] long? lastSequence,
        CancellationToken cancellationToken)
    {
        if (!HttpContext.WebSockets.IsWebSocketRequest)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsync("WebSocket request required.", cancellationToken);
            return;
        }

        await EnsureHydratedAsync(runId, cancellationToken);

        using var socket = await HttpContext.WebSockets.AcceptWebSocketAsync();
        var sequence = Math.Max(0, lastSequence ?? 0);

        using var monitorCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var monitorTask = MonitorRunEventsAsync(socket, runId, () => sequence, next => sequence = next, monitorCts.Token);

        var buffer = new byte[8 * 1024];
        while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            WebSocketReceiveResult result;
            using var payload = new MemoryStream();
            do
            {
                result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    monitorCts.Cancel();
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "closed", cancellationToken);
                    await AwaitMonitorAsync(monitorTask);
                    return;
                }

                payload.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);

            var text = Encoding.UTF8.GetString(payload.ToArray());
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            TryHandleClientMessage(runId, text, ref sequence);
        }

        monitorCts.Cancel();
        await AwaitMonitorAsync(monitorTask);
    }

    private async Task EnsureHydratedAsync(string runId, CancellationToken cancellationToken)
    {
        if (_eventStream.GetSnapshot(runId) is not null)
        {
            return;
        }

        var session = await _testRunService.GetRunSessionAsync(runId, cancellationToken);
        _eventStream.WarmFromSession(session);
    }

    private async Task MonitorRunEventsAsync(
        WebSocket socket,
        string runId,
        Func<long> readSequence,
        Action<long> writeSequence,
        CancellationToken cancellationToken)
    {
        var lastHeartbeatAt = DateTimeOffset.UtcNow;
        while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
        {
            var events = _eventStream.GetEventsSince(runId, readSequence(), 256);
            if (events.Count > 0)
            {
                foreach (var evt in events)
                {
                    await SendAsync(socket, evt, cancellationToken);
                    writeSequence(evt.Sequence);
                }
                lastHeartbeatAt = DateTimeOffset.UtcNow;
            }
            else if (DateTimeOffset.UtcNow - lastHeartbeatAt >= TimeSpan.FromSeconds(15))
            {
                _eventStream.PublishHeartbeat(runId, DateTimeOffset.UtcNow);
                lastHeartbeatAt = DateTimeOffset.UtcNow;
            }

            await Task.Delay(120, cancellationToken);
        }
    }

    private void TryHandleClientMessage(
        string runId,
        string text,
        ref long sequence)
    {
        try
        {
            using var json = JsonDocument.Parse(text);
            if (json.RootElement.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            var type = json.RootElement.TryGetProperty("type", out var typeValue) && typeValue.ValueKind == JsonValueKind.String
                ? typeValue.GetString()
                : string.Empty;
            if (string.Equals(type, "resumeFrom", StringComparison.OrdinalIgnoreCase))
            {
                if (json.RootElement.TryGetProperty("lastSequence", out var sequenceValue) && sequenceValue.TryGetInt64(out var parsed))
                {
                    sequence = Math.Max(0, parsed);
                }
                return;
            }

            if (string.Equals(type, "ping", StringComparison.OrdinalIgnoreCase))
            {
                _eventStream.PublishHeartbeat(runId, DateTimeOffset.UtcNow);
            }
        }
        catch (JsonException)
        {
            // Ignore invalid client payloads to keep runtime stream stable.
        }
    }

    private static async Task AwaitMonitorAsync(Task monitorTask)
    {
        try
        {
            await monitorTask;
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static async Task SendAsync(WebSocket socket, object payload, CancellationToken cancellationToken)
    {
        if (socket.State != WebSocketState.Open)
        {
            return;
        }

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken);
    }
}
