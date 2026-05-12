using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Runtime.Debug;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Microflows.Controllers;

[ApiController]
public sealed class MicroflowDebugController : ControllerBase
{
    private const int MaxConcurrentDebugSessions = 256;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IDebugSessionStore _sessions;
    private readonly IMicroflowDebugCoordinator _debugCoordinator;
    private readonly IMicroflowRequestContextAccessor _requestContextAccessor;

    public MicroflowDebugController(
        IDebugSessionStore sessions,
        IMicroflowDebugCoordinator debugCoordinator,
        IMicroflowRequestContextAccessor requestContextAccessor)
    {
        _sessions = sessions;
        _debugCoordinator = debugCoordinator;
        _requestContextAccessor = requestContextAccessor;
    }

    [HttpGet("/api/debug/microflow/{microflowId}")]
    public async Task Connect(string microflowId, [FromQuery] string? sessionId, CancellationToken cancellationToken)
    {
        if (!HttpContext.WebSockets.IsWebSocketRequest)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsync("WebSocket request required.", cancellationToken);
            return;
        }

        if (string.IsNullOrWhiteSpace(sessionId))
        {
            sessionId = Guid.NewGuid().ToString("N");
        }

        if (_sessions.SessionCount >= MaxConcurrentDebugSessions && _sessions.Get(sessionId) is null)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await HttpContext.Response.WriteAsync("Debug session limit exceeded.", cancellationToken);
            return;
        }

        var current = _requestContextAccessor.Current;
        var session = EnsureSession(sessionId, microflowId, current);
        if (session is null)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
            await HttpContext.Response.WriteAsync("Forbidden.", cancellationToken);
            return;
        }

        using var socket = await HttpContext.WebSockets.AcceptWebSocketAsync();
        await SendSessionStatusAsync(socket, session, "initialized", cancellationToken);
        await SendStateSyncAsync(socket, session, cancellationToken);

        using var monitorCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var monitorTask = MonitorSessionAsync(socket, session.Id, monitorCts.Token);

        var buffer = new byte[64 * 1024];
        while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            WebSocketReceiveResult receiveResult;
            using var payload = new MemoryStream();
            do
            {
                receiveResult = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                if (receiveResult.MessageType == WebSocketMessageType.Close)
                {
                    monitorCts.Cancel();
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "closed", cancellationToken);
                    try
                    {
                        await monitorTask;
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    return;
                }

                payload.Write(buffer, 0, receiveResult.Count);
            } while (!receiveResult.EndOfMessage);

            var text = Encoding.UTF8.GetString(payload.ToArray());
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            ClientMessage? clientMessage;
            try
            {
                clientMessage = JsonSerializer.Deserialize<ClientMessage>(text, JsonOptions);
            }
            catch (JsonException)
            {
                await SendAsync(socket, new
                {
                    type = "error",
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    data = new
                    {
                        nodeId = session.CurrentNodeObjectId ?? string.Empty,
                        message = "Invalid debug payload.",
                        errorType = "client-payload-invalid"
                    }
                }, cancellationToken);
                continue;
            }

            if (clientMessage is null || string.IsNullOrWhiteSpace(clientMessage.Type))
            {
                continue;
            }

            session = _sessions.Get(session.Id) ?? session;
            var updated = await HandleClientMessageAsync(session, clientMessage, socket, cancellationToken);
            if (updated is not null)
            {
                session = updated;
                await SendSessionStatusAsync(socket, session, MapWsSessionStatus(session.Status), cancellationToken);
                await SendStateSyncAsync(socket, session, cancellationToken);
            }
        }

        monitorCts.Cancel();
        try
        {
            await monitorTask;
        }
        catch (OperationCanceledException)
        {
        }
    }

    private MicroflowDebugSession? EnsureSession(string sessionId, string microflowId, MicroflowRequestContext current)
    {
        var existing = _sessions.Get(sessionId);
        if (existing is null)
        {
            var created = new MicroflowDebugSession
            {
                Id = sessionId,
                MicroflowId = microflowId,
                TenantId = current.TenantId,
                WorkspaceId = current.WorkspaceId,
                CreatedBy = current.UserId,
                Status = MicroflowDebugSessionLifecycle.Created,
                LastCommand = DebugCommandKind.Pause
            };
            return _sessions.Upsert(created);
        }

        var workspaceMismatch = !string.IsNullOrWhiteSpace(existing.WorkspaceId)
            && !string.IsNullOrWhiteSpace(current.WorkspaceId)
            && !string.Equals(existing.WorkspaceId, current.WorkspaceId, StringComparison.Ordinal);
        var tenantMismatch = !string.IsNullOrWhiteSpace(existing.TenantId)
            && !string.IsNullOrWhiteSpace(current.TenantId)
            && !string.Equals(existing.TenantId, current.TenantId, StringComparison.Ordinal);
        var userMismatch = !string.IsNullOrWhiteSpace(existing.CreatedBy)
            && !string.IsNullOrWhiteSpace(current.UserId)
            && !string.Equals(existing.CreatedBy, current.UserId, StringComparison.Ordinal);

        if (workspaceMismatch || tenantMismatch || userMismatch)
        {
            return null;
        }

        if (!string.Equals(existing.MicroflowId, microflowId, StringComparison.Ordinal))
        {
            existing = existing with { MicroflowId = microflowId };
            existing = _sessions.Upsert(existing);
        }

        return existing;
    }

    private async Task<MicroflowDebugSession?> HandleClientMessageAsync(
        MicroflowDebugSession session,
        ClientMessage message,
        WebSocket socket,
        CancellationToken cancellationToken)
    {
        var normalizedType = NormalizeType(message.Type);
        switch (normalizedType)
        {
            case "ping":
            {
                var sequence = ReadLong(message.Data, "sequence") ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                await SendAsync(socket, new
                {
                    type = "pong",
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    data = new { sequence }
                }, cancellationToken);
                return session;
            }
            case "set-breakpoint":
            {
                var nodeId = ReadString(message.Data, "nodeId");
                if (string.IsNullOrWhiteSpace(nodeId))
                {
                    return session;
                }

                var condition = ReadString(message.Data, "condition");
                var enabled = ReadBool(message.Data, "enabled") ?? true;
                var breakpointId = $"bp:{nodeId}";
                var breakpoint = new BreakpointDescriptor(
                    breakpointId,
                    nodeId,
                    BreakpointScope.Node,
                    false)
                {
                    Enabled = enabled,
                    HitTarget = null
                };

                var updated = _debugCoordinator.UpsertBreakpoint(session.Id, breakpoint);
                if (!string.IsNullOrWhiteSpace(condition))
                {
                    var conditional = new ConditionalBreakpointDescriptor(
                        $"cbp:{nodeId}",
                        nodeId,
                        condition,
                        0,
                        BreakpointSuspendPolicy.All,
                        false,
                        false)
                    {
                        Enabled = enabled,
                        Scope = BreakpointScope.Node
                    };
                    var baseSession = updated ?? _sessions.Get(session.Id) ?? session;
                    updated = _sessions.Upsert(baseSession with
                    {
                        ConditionalBreakpoints = UpsertConditionalBreakpoint(baseSession.ConditionalBreakpoints, conditional),
                        Trace = baseSession.Trace.Concat(new[]
                        {
                            new DebugTraceEvent
                            {
                                Kind = "breakpoint",
                                RunId = baseSession.BoundEngineRunId ?? baseSession.RunId,
                                NodeObjectId = nodeId,
                                Message = $"upsert-conditional:{nodeId}"
                            }
                        }).ToArray()
                    });
                }

                return updated ?? _sessions.Get(session.Id);
            }
            case "remove-breakpoint":
            {
                var nodeId = ReadString(message.Data, "nodeId");
                if (string.IsNullOrWhiteSpace(nodeId))
                {
                    return session;
                }

                var updated = _debugCoordinator.RemoveBreakpoint(session.Id, $"bp:{nodeId}");
                var baseSession = updated ?? _sessions.Get(session.Id) ?? session;
                updated = _sessions.Upsert(baseSession with
                {
                    ConditionalBreakpoints = baseSession.ConditionalBreakpoints
                        .Where(item => !string.Equals(item.MicroflowObjectId, nodeId, StringComparison.Ordinal))
                        .ToArray()
                });
                return updated ?? _sessions.Get(session.Id);
            }
            case "toggle-breakpoint":
            {
                var nodeId = ReadString(message.Data, "nodeId");
                var enabled = ReadBool(message.Data, "enabled") ?? true;
                if (string.IsNullOrWhiteSpace(nodeId))
                {
                    return session;
                }

                var hit = session.Breakpoints.FirstOrDefault(item => string.Equals(item.MicroflowObjectId, nodeId, StringComparison.Ordinal));
                if (hit is null)
                {
                    return session;
                }

                var updated = _debugCoordinator.UpsertBreakpoint(session.Id, hit with { Enabled = enabled });
                var baseSession = updated ?? _sessions.Get(session.Id) ?? session;
                updated = _sessions.Upsert(baseSession with
                {
                    ConditionalBreakpoints = baseSession.ConditionalBreakpoints
                        .Select(item => string.Equals(item.MicroflowObjectId, nodeId, StringComparison.Ordinal)
                            ? item with { Enabled = enabled }
                            : item)
                        .ToArray()
                });
                return updated ?? _sessions.Get(session.Id);
            }
            case "get-variable-details":
            {
                var requestId = ReadString(message.Data, "requestId") ?? Guid.NewGuid().ToString("N");
                var variableName = ReadString(message.Data, "variableName") ?? string.Empty;
                var variable = session.Variables.FirstOrDefault(item => string.Equals(item.Name, variableName, StringComparison.Ordinal));
                await SendAsync(socket, new
                {
                    type = "variable-details",
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    data = new
                    {
                        requestId,
                        variableName,
                        value = variable?.RawValueJson ?? variable?.ValuePreview
                    }
                }, cancellationToken);
                return session;
            }
            case "set-variable":
            {
                var variableName = ReadString(message.Data, "variableName");
                if (string.IsNullOrWhiteSpace(variableName))
                {
                    return session;
                }

                var value = ReadJsonRaw(message.Data, "value");
                var variables = session.Variables.ToList();
                var index = variables.FindIndex(item => string.Equals(item.Name, variableName, StringComparison.Ordinal));
                if (index >= 0)
                {
                    variables[index] = variables[index] with
                    {
                        ValuePreview = value,
                        RawValueJson = value,
                        RedactionApplied = false
                    };
                    return _sessions.Upsert(session with { Variables = variables });
                }

                variables.Add(new DebugVariableSnapshot
                {
                    Name = variableName,
                    Type = "unknown",
                    ValuePreview = value,
                    RawValueJson = value,
                    RedactionApplied = false
                });
                return _sessions.Upsert(session with { Variables = variables });
            }
            default:
            {
                var command = MapDebugCommand(normalizedType, message.Data);
                var updated = _debugCoordinator.ApplyCommand(session.Id, command);
                return updated ?? _sessions.Get(session.Id);
            }
        }
    }

    private async Task MonitorSessionAsync(WebSocket socket, string sessionId, CancellationToken cancellationToken)
    {
        DateTimeOffset? lastUpdatedAt = null;
        int lastTraceCount = 0;
        var lastPingAt = DateTimeOffset.UtcNow;
        string? lastPauseNodeId = null;
        DateTimeOffset? lastPauseUpdatedAt = null;

        while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
        {
            var session = _sessions.Get(sessionId);
            if (session is null)
            {
                break;
            }

            if (lastUpdatedAt is null || session.UpdatedAt > lastUpdatedAt.Value)
            {
                await SendSessionStatusAsync(socket, session, MapWsSessionStatus(session.Status), cancellationToken);
                await SendStateSyncAsync(socket, session, cancellationToken);
                lastUpdatedAt = session.UpdatedAt;
            }

            if (session.Trace.Count > lastTraceCount)
            {
                foreach (var trace in session.Trace.Skip(lastTraceCount))
                {
                    if (string.Equals(trace.Kind, "breakpoint", StringComparison.OrdinalIgnoreCase))
                    {
                        await SendAsync(socket, new
                        {
                            type = "breakpoint",
                            id = trace.Id,
                            timestamp = trace.CreatedAt.ToUnixTimeMilliseconds(),
                            data = new
                            {
                                nodeId = trace.NodeObjectId ?? session.CurrentSafePoint?.NodeObjectId ?? session.CurrentNodeObjectId ?? string.Empty,
                                conditionMet = true,
                                variables = session.Variables.Select(item => new
                                {
                                    name = item.Name,
                                    type = item.Type,
                                    valuePreview = item.ValuePreview
                                }).ToArray(),
                                callStack = session.CallStack.Select(item => new
                                {
                                    id = item.Id,
                                    microflowId = item.MicroflowId,
                                    runId = item.RunId,
                                    callerObjectId = item.CallerObjectId,
                                    callerActionId = item.CallerActionId,
                                    depth = item.Depth,
                                    status = item.Status
                                }).ToArray()
                            }
                        }, cancellationToken);
                        continue;
                    }
                    await SendTraceEventAsync(socket, trace, cancellationToken);
                }
                lastTraceCount = session.Trace.Count;
            }

            if (string.Equals(session.Status, MicroflowDebugSessionLifecycle.Paused, StringComparison.OrdinalIgnoreCase))
            {
                var pauseNodeId = session.CurrentSafePoint?.NodeObjectId ?? session.CurrentNodeObjectId ?? string.Empty;
                if (!string.Equals(lastPauseNodeId, pauseNodeId, StringComparison.Ordinal)
                    || lastPauseUpdatedAt is null
                    || session.UpdatedAt > lastPauseUpdatedAt.Value)
                {
                    await SendAsync(socket, new
                    {
                        type = "paused",
                        id = Guid.NewGuid().ToString("N"),
                        timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        data = new
                        {
                            reason = string.Equals(session.LastCommand, DebugCommandKind.Pause, StringComparison.OrdinalIgnoreCase)
                                ? "user-requested"
                                : "step-completed",
                            currentNodeId = pauseNodeId,
                            variables = session.Variables.Select(item => new
                            {
                                name = item.Name,
                                type = item.Type,
                                valuePreview = item.ValuePreview
                            }).ToArray(),
                            callStack = session.CallStack.Select(item => new
                            {
                                id = item.Id,
                                microflowId = item.MicroflowId,
                                runId = item.RunId,
                                callerObjectId = item.CallerObjectId,
                                callerActionId = item.CallerActionId,
                                depth = item.Depth,
                                status = item.Status
                            }).ToArray()
                        }
                    }, cancellationToken);

                    lastPauseNodeId = pauseNodeId;
                    lastPauseUpdatedAt = session.UpdatedAt;
                }
            }

            if (DateTimeOffset.UtcNow - lastPingAt >= TimeSpan.FromSeconds(30))
            {
                var sequence = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                await SendAsync(socket, new
                {
                    type = "ping",
                    id = Guid.NewGuid().ToString("N"),
                    timestamp = sequence,
                    data = new
                    {
                        sequence
                    }
                }, cancellationToken);
                lastPingAt = DateTimeOffset.UtcNow;
            }

            if (session.Status is MicroflowDebugSessionLifecycle.Completed
                or MicroflowDebugSessionLifecycle.Cancelled
                or MicroflowDebugSessionLifecycle.Failed
                or MicroflowDebugSessionLifecycle.Expired)
            {
                await SendAsync(socket, new
                {
                    type = "complete",
                    id = Guid.NewGuid().ToString("N"),
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    data = new
                    {
                        durationMs = 0,
                        status = session.Status is MicroflowDebugSessionLifecycle.Completed ? "success" : session.Status is MicroflowDebugSessionLifecycle.Cancelled ? "cancelled" : "error"
                    }
                }, cancellationToken);
                break;
            }

            await Task.Delay(120, cancellationToken);
        }
    }

    private static async Task SendTraceEventAsync(WebSocket socket, DebugTraceEvent trace, CancellationToken cancellationToken)
    {
        var safePointPhase = trace.Kind == "safePoint"
            ? (trace.Message?.Split(':', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty)
            : string.Empty;
        var messageType = trace.Kind switch
        {
            "safePoint" when safePointPhase.StartsWith("after", StringComparison.OrdinalIgnoreCase) => "node-exit",
            "safePoint" => "node-enter",
            "command" => "paused",
            "breakpoint" => "breakpoint",
            "timeout" => "error",
            "mutation" => "paused",
            _ => "session-status"
        };

        await SendAsync(socket, new
        {
            type = messageType,
            id = trace.Id,
            timestamp = trace.CreatedAt.ToUnixTimeMilliseconds(),
            data = new
            {
                nodeId = trace.NodeObjectId,
                flowId = trace.FlowId,
                branchId = trace.BranchId,
                message = trace.Message,
                errorType = trace.Kind
            }
        }, cancellationToken);
    }
    private static DebugCommand MapDebugCommand(string messageType, Dictionary<string, JsonElement>? data)
    {
        var command = messageType switch
        {
            "step-over" => DebugCommandKind.StepOver,
            "step-into" => DebugCommandKind.StepInto,
            "step-out" => DebugCommandKind.StepOut,
            "continue" => DebugCommandKind.Continue,
            "continue-all" => DebugCommandKind.Continue,
            "pause" => DebugCommandKind.Pause,
            "run-to-node" => DebugCommandKind.RunToNode,
            "run-to-cursor" => DebugCommandKind.RunToCursor,
            "stop" => DebugCommandKind.Stop,
            _ => DebugCommandKind.Pause
        };

        return new DebugCommand
        {
            Command = command,
            TargetNodeObjectId = ReadString(data, "nodeId") ?? ReadString(data, "targetNodeObjectId"),
            TargetFlowId = ReadString(data, "flowId") ?? ReadString(data, "targetFlowId")
        };
    }

    private static async Task SendSessionStatusAsync(
        WebSocket socket,
        MicroflowDebugSession session,
        string status,
        CancellationToken cancellationToken)
    {
        await SendAsync(socket, new
        {
            type = "session-status",
            id = Guid.NewGuid().ToString("N"),
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            data = new
            {
                status,
                sessionId = session.Id
            }
        }, cancellationToken);
    }

    private static async Task SendStateSyncAsync(WebSocket socket, MicroflowDebugSession session, CancellationToken cancellationToken)
    {
        var nodeStatuses = new Dictionary<string, string>(StringComparer.Ordinal);
        var executedEdgeIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var trace in session.Trace)
        {
            if (!string.IsNullOrWhiteSpace(trace.NodeObjectId))
            {
                nodeStatuses[trace.NodeObjectId!] = trace.Kind switch
                {
                    "timeout" => "error",
                    "mutation" => "success",
                    _ => "running"
                };
            }
            if (!string.IsNullOrWhiteSpace(trace.FlowId))
            {
                executedEdgeIds.Add(trace.FlowId!);
            }
        }

        await SendAsync(socket, new
        {
            type = "state-sync",
            id = Guid.NewGuid().ToString("N"),
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            data = new
            {
                nodeStatuses,
                executedEdgeIds = executedEdgeIds.ToArray(),
                variables = session.Variables.Select(item => new
                {
                    name = item.Name,
                    type = item.Type,
                    valuePreview = item.ValuePreview,
                    value = item.ValuePreview
                }).ToArray(),
                breakpoints = session.Breakpoints.Select(item => new
                {
                    nodeId = item.MicroflowObjectId,
                    enabled = item.Enabled
                })
                .Concat(session.ConditionalBreakpoints.Select(item => new
                {
                    nodeId = item.MicroflowObjectId,
                    enabled = item.Enabled
                }))
                .ToArray(),
                callStack = session.CallStack.Select(item => new
                {
                    id = item.Id,
                    microflowId = item.MicroflowId,
                    runId = item.RunId,
                    callerObjectId = item.CallerObjectId,
                    callerActionId = item.CallerActionId,
                    depth = item.Depth,
                    status = item.Status
                }).ToArray()
            }
        }, cancellationToken);
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

    private static string NormalizeType(string? type)
        => string.IsNullOrWhiteSpace(type)
            ? string.Empty
            : type.Trim().ToLowerInvariant();

    private static string MapWsSessionStatus(string status)
        => status switch
        {
            MicroflowDebugSessionLifecycle.Created => "initialized",
            MicroflowDebugSessionLifecycle.Running => "running",
            MicroflowDebugSessionLifecycle.Paused => "paused",
            MicroflowDebugSessionLifecycle.Completed => "completed",
            MicroflowDebugSessionLifecycle.Cancelled => "completed",
            MicroflowDebugSessionLifecycle.Failed => "completed",
            MicroflowDebugSessionLifecycle.Expired => "completed",
            _ => "running"
        };

    private static string? ReadString(Dictionary<string, JsonElement>? data, string key)
    {
        if (data is null || !data.TryGetValue(key, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    private static bool? ReadBool(Dictionary<string, JsonElement>? data, string key)
    {
        if (data is null || !data.TryGetValue(key, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(value.GetString(), out var parsed) => parsed,
            _ => null
        };
    }

    private static long? ReadLong(Dictionary<string, JsonElement>? data, string key)
    {
        if (data is null || !data.TryGetValue(key, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt64(out var number) => number,
            JsonValueKind.String when long.TryParse(value.GetString(), out var parsed) => parsed,
            _ => null
        };
    }

    private static string? ReadJsonRaw(Dictionary<string, JsonElement>? data, string key)
    {
        if (data is null || !data.TryGetValue(key, out var value))
        {
            return null;
        }

        return value.GetRawText();
    }

    private static IReadOnlyList<ConditionalBreakpointDescriptor> UpsertConditionalBreakpoint(
        IReadOnlyList<ConditionalBreakpointDescriptor> breakpoints,
        ConditionalBreakpointDescriptor breakpoint)
        => breakpoints
            .Where(item => !string.Equals(item.Id, breakpoint.Id, StringComparison.Ordinal))
            .Concat(new[] { breakpoint })
            .ToArray();

    private sealed record ClientMessage
    {
        public string Type { get; init; } = string.Empty;

        public Dictionary<string, JsonElement>? Data { get; init; }
    }
}




