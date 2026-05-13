using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Atlas.Application.Microflows.Models;

namespace Atlas.Application.Microflows.Runtime;

public sealed record MicroflowRuntimeWsEvent
{
    [JsonPropertyName("eventId")]
    public string EventId { get; init; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("runId")]
    public string RunId { get; init; } = string.Empty;

    [JsonPropertyName("sequence")]
    public long Sequence { get; init; }

    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("objectId")]
    public string? ObjectId { get; init; }

    [JsonPropertyName("flowId")]
    public string? FlowId { get; init; }

    [JsonPropertyName("payload")]
    public object? Payload { get; init; }
}

public sealed record MicroflowRuntimeOverlayNode
{
    [JsonPropertyName("objectId")]
    public string ObjectId { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; init; } = "idle";

    [JsonPropertyName("inputSummary")]
    public IReadOnlyList<object>? InputSummary { get; init; }

    [JsonPropertyName("outputSummary")]
    public IReadOnlyList<object>? OutputSummary { get; init; }

    [JsonPropertyName("variableDeltaSummary")]
    public IReadOnlyList<object>? VariableDeltaSummary { get; init; }

    [JsonPropertyName("selectedCaseLabel")]
    public string? SelectedCaseLabel { get; init; }

    [JsonPropertyName("selectedCaseValue")]
    public JsonElement? SelectedCaseValue { get; init; }

    [JsonPropertyName("loopIteration")]
    public object? LoopIteration { get; init; }

    [JsonPropertyName("gatewaySummary")]
    public object? GatewaySummary { get; init; }

    [JsonPropertyName("durationMs")]
    public long? DurationMs { get; init; }

    [JsonPropertyName("error")]
    public object? Error { get; init; }
}

public sealed record MicroflowRuntimeOverlayFlow
{
    [JsonPropertyName("flowId")]
    public string FlowId { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; init; } = "idle";

    [JsonPropertyName("selectedCaseValue")]
    public JsonElement? SelectedCaseValue { get; init; }

    [JsonPropertyName("visitedAt")]
    public DateTimeOffset? VisitedAt { get; init; }
}

public sealed record MicroflowRuntimeOverlaySnapshot
{
    [JsonPropertyName("runId")]
    public string RunId { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; init; } = "idle";

    [JsonPropertyName("currentObjectId")]
    public string? CurrentObjectId { get; init; }

    [JsonPropertyName("lastSequence")]
    public long LastSequence { get; init; }

    [JsonPropertyName("result")]
    public JsonElement? Result { get; init; }

    [JsonPropertyName("nodeOverlays")]
    public IReadOnlyDictionary<string, MicroflowRuntimeOverlayNode> NodeOverlays { get; init; }
        = new Dictionary<string, MicroflowRuntimeOverlayNode>(StringComparer.Ordinal);

    [JsonPropertyName("flowOverlays")]
    public IReadOnlyDictionary<string, MicroflowRuntimeOverlayFlow> FlowOverlays { get; init; }
        = new Dictionary<string, MicroflowRuntimeOverlayFlow>(StringComparer.Ordinal);

    [JsonPropertyName("events")]
    public IReadOnlyList<MicroflowRuntimeWsEvent> Events { get; init; } = Array.Empty<MicroflowRuntimeWsEvent>();
}

public interface IMicroflowRuntimeWsEventStream
{
    void StartRun(string runId, string? resourceId, DateTimeOffset startedAt);

    void PublishFrame(MicroflowTraceFrameDto frame);

    void CompleteRun(string runId, string status, DateTimeOffset completedAt, JsonElement? result, MicroflowRuntimeErrorDto? error);

    void PublishHeartbeat(string runId, DateTimeOffset timestamp);

    IReadOnlyList<MicroflowRuntimeWsEvent> GetEventsSince(string runId, long lastSequence, int maxCount = 256);

    MicroflowRuntimeOverlaySnapshot? GetSnapshot(string runId, long lastSequence = 0);

    void WarmFromSession(MicroflowRunSessionDto session);
}

public sealed class InMemoryMicroflowRuntimeWsEventStream : IMicroflowRuntimeWsEventStream
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const int MaxEventsPerRun = 6000;
    private readonly ConcurrentDictionary<string, RunBuffer> _runs = new(StringComparer.Ordinal);

    public void StartRun(string runId, string? resourceId, DateTimeOffset startedAt)
    {
        if (string.IsNullOrWhiteSpace(runId))
        {
            return;
        }

        var buffer = _runs.GetOrAdd(runId, static id => new RunBuffer(id));
        lock (buffer.SyncRoot)
        {
            if (buffer.Started)
            {
                return;
            }

            buffer.Started = true;
            buffer.Status = "running";
            buffer.StartedAt = startedAt;
            AppendEventLocked(buffer, "run.started", startedAt, payload: new
            {
                resourceId,
                status = "running"
            });
        }
    }

    public void PublishFrame(MicroflowTraceFrameDto frame)
    {
        if (string.IsNullOrWhiteSpace(frame.RunId))
        {
            return;
        }

        var buffer = _runs.GetOrAdd(frame.RunId, static id => new RunBuffer(id));
        lock (buffer.SyncRoot)
        {
            if (!buffer.Started)
            {
                buffer.Started = true;
                buffer.Status = "running";
                buffer.StartedAt = frame.StartedAt;
                AppendEventLocked(buffer, "run.started", frame.StartedAt, payload: new { status = "running" });
            }

            AppendEventLocked(buffer, "node.started", frame.StartedAt, frame.ObjectId, payload: new
            {
                caption = frame.ObjectId,
                nodeKind = frame.NodeKind,
                actionKind = frame.ActionKind,
            });

            if (!string.IsNullOrWhiteSpace(frame.IncomingFlowId))
            {
                AppendEventLocked(buffer, "edge.running", frame.StartedAt, flowId: frame.IncomingFlowId, payload: new { status = "running" });
            }

            var inputSummary = BuildValueSummary(frame.InputVariables);
            if (inputSummary.Count > 0)
            {
                AppendEventLocked(buffer, "node.inputResolved", frame.StartedAt, frame.ObjectId, payload: new
                {
                    inputSummary
                });
            }

            AppendEdgeVisited(buffer, frame.IncomingFlowId, frame.StartedAt);
            AppendEdgeVisited(buffer, frame.OutgoingFlowId, frame.EndedAt ?? frame.StartedAt);

            var outputSummary = BuildValueSummary(frame.OutputVariables);
            var variableDeltaSummary = BuildVariableDeltaSummary(frame.VariableDelta, frame.OutputVariables);
            if (outputSummary.Count > 0 || variableDeltaSummary.Count > 0)
            {
                AppendEventLocked(buffer, "node.outputProduced", frame.EndedAt ?? frame.StartedAt, frame.ObjectId, payload: new
                {
                    outputSummary,
                    variableDeltaSummary
                });
            }

            if (variableDeltaSummary.Count > 0)
            {
                AppendEventLocked(buffer, "variable.delta", frame.EndedAt ?? frame.StartedAt, frame.ObjectId, payload: new
                {
                    variableDeltaSummary
                });
            }

            PublishBranchEventsLocked(buffer, frame);
            PublishLoopEventsLocked(buffer, frame);
            PublishGatewayEventsLocked(buffer, frame);

            if (string.Equals(frame.Status, "failed", StringComparison.OrdinalIgnoreCase) || frame.Error is not null)
            {
                var failedFlowId = !string.IsNullOrWhiteSpace(frame.OutgoingFlowId) ? frame.OutgoingFlowId : frame.IncomingFlowId;
                if (!string.IsNullOrWhiteSpace(failedFlowId))
                {
                    AppendEventLocked(buffer, "edge.failed", frame.EndedAt ?? frame.StartedAt, flowId: failedFlowId, payload: new { status = "failed" });
                }
                AppendEventLocked(buffer, "node.failed", frame.EndedAt ?? frame.StartedAt, frame.ObjectId, payload: new
                {
                    durationMs = frame.DurationMs,
                    error = frame.Error is null
                        ? null
                        : new
                        {
                            code = frame.Error.Code,
                            message = frame.Error.Message
                        }
                });
            }
            else
            {
                AppendEventLocked(buffer, "node.completed", frame.EndedAt ?? frame.StartedAt, frame.ObjectId, payload: new
                {
                    status = NormalizeNodeStatus(frame.Status),
                    durationMs = frame.DurationMs
                });
            }

            if (frame.ErrorHandlerVisited == true && !string.IsNullOrWhiteSpace(frame.IncomingFlowId))
            {
                AppendEventLocked(buffer, "edge.errorHandlerVisited", frame.EndedAt ?? frame.StartedAt, flowId: frame.IncomingFlowId, payload: new { status = "errorHandlerVisited" });
            }
        }
    }

    public void CompleteRun(string runId, string status, DateTimeOffset completedAt, JsonElement? result, MicroflowRuntimeErrorDto? error)
    {
        if (string.IsNullOrWhiteSpace(runId))
        {
            return;
        }

        var buffer = _runs.GetOrAdd(runId, static id => new RunBuffer(id));
        lock (buffer.SyncRoot)
        {
            buffer.Status = string.Equals(status, "success", StringComparison.OrdinalIgnoreCase) ? "completed" : "failed";
            if (error is not null || string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase))
            {
                buffer.Result = null;
                AppendEventLocked(buffer, "run.failed", completedAt, objectId: buffer.CurrentObjectId, payload: new
                {
                    status = "failed",
                    error = error is null
                        ? null
                        : new
                        {
                            code = error.Code,
                            message = error.Message
                        }
                });
                return;
            }

            buffer.Result = result;
            AppendEventLocked(buffer, "run.completed", completedAt, objectId: buffer.CurrentObjectId, payload: new
            {
                status = "succeeded",
                result,
                durationMs = buffer.StartedAt.HasValue
                    ? Math.Max(0, (long)(completedAt - buffer.StartedAt.Value).TotalMilliseconds)
                    : 0L,
            });
        }
    }

    public void PublishHeartbeat(string runId, DateTimeOffset timestamp)
    {
        if (string.IsNullOrWhiteSpace(runId))
        {
            return;
        }

        var buffer = _runs.GetOrAdd(runId, static id => new RunBuffer(id));
        lock (buffer.SyncRoot)
        {
            AppendEventLocked(buffer, "heartbeat", timestamp, payload: new { alive = true });
        }
    }

    public IReadOnlyList<MicroflowRuntimeWsEvent> GetEventsSince(string runId, long lastSequence, int maxCount = 256)
    {
        if (!_runs.TryGetValue(runId, out var buffer))
        {
            return Array.Empty<MicroflowRuntimeWsEvent>();
        }

        lock (buffer.SyncRoot)
        {
            return buffer.Events
                .Where(evt => evt.Sequence > lastSequence)
                .Take(Math.Max(1, maxCount))
                .ToArray();
        }
    }

    public MicroflowRuntimeOverlaySnapshot? GetSnapshot(string runId, long lastSequence = 0)
    {
        if (!_runs.TryGetValue(runId, out var buffer))
        {
            return null;
        }

        lock (buffer.SyncRoot)
        {
            var events = buffer.Events.Where(evt => evt.Sequence > lastSequence).ToArray();
            return new MicroflowRuntimeOverlaySnapshot
            {
                RunId = runId,
                Status = buffer.Status,
                CurrentObjectId = buffer.CurrentObjectId,
                LastSequence = buffer.Sequence,
                Result = buffer.Result,
                NodeOverlays = new Dictionary<string, MicroflowRuntimeOverlayNode>(buffer.NodeOverlays, StringComparer.Ordinal),
                FlowOverlays = new Dictionary<string, MicroflowRuntimeOverlayFlow>(buffer.FlowOverlays, StringComparer.Ordinal),
                Events = events
            };
        }
    }

    public void WarmFromSession(MicroflowRunSessionDto session)
    {
        if (string.IsNullOrWhiteSpace(session.Id))
        {
            return;
        }

        if (_runs.ContainsKey(session.Id))
        {
            return;
        }

        StartRun(session.Id, session.ResourceId, session.StartedAt);
        foreach (var frame in session.Trace)
        {
            PublishFrame(frame);
        }

        CompleteRun(session.Id, session.Status, session.EndedAt ?? DateTimeOffset.UtcNow, session.Output, session.Error);
    }

    private static IReadOnlyList<object> BuildValueSummary(JsonElement? variables)
    {
        if (!variables.HasValue || variables.Value.ValueKind != JsonValueKind.Object)
        {
            return Array.Empty<object>();
        }

        var list = new List<object>();
        foreach (var property in variables.Value.EnumerateObject())
        {
            var name = property.Name;
            if (property.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var preview = ReadString(property.Value, "valuePreview") ?? CompactPreview(ReadElement(property.Value, "rawValue"));
            var type = ReadDataType(property.Value);
            list.Add(new
            {
                name,
                type,
                preview
            });
        }

        return list;
    }

    private static IReadOnlyList<object> BuildVariableDeltaSummary(JsonElement? delta, JsonElement? outputVariables)
    {
        if (!delta.HasValue || delta.Value.ValueKind != JsonValueKind.Object)
        {
            return Array.Empty<object>();
        }

        var outputMap = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (outputVariables.HasValue && outputVariables.Value.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in outputVariables.Value.EnumerateObject())
            {
                outputMap[property.Name] = property.Value.Clone();
            }
        }

        var result = new List<object>();
        AddDelta(result, "added", "added", delta.Value, outputMap);
        AddDelta(result, "changed", "changed", delta.Value, outputMap);
        AddDelta(result, "removed", "removed", delta.Value, outputMap);
        return result;
    }

    private static void AddDelta(List<object> result, string sourceKey, string kind, JsonElement delta, IReadOnlyDictionary<string, JsonElement> outputMap)
    {
        if (!delta.TryGetProperty(sourceKey, out var names) || names.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var nameElement in names.EnumerateArray())
        {
            if (nameElement.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var name = nameElement.GetString() ?? string.Empty;
            outputMap.TryGetValue(name, out var value);
            var preview = value.ValueKind == JsonValueKind.Undefined
                ? string.Empty
                : ReadString(value, "valuePreview") ?? CompactPreview(ReadElement(value, "rawValue"));
            result.Add(new
            {
                kind,
                name,
                afterPreview = preview
            });
        }
    }

    private static void PublishBranchEventsLocked(RunBuffer buffer, MicroflowTraceFrameDto frame)
    {
        if (!frame.SelectedCaseValue.HasValue || string.IsNullOrWhiteSpace(frame.OutgoingFlowId))
        {
            return;
        }

        var label = CompactPreview(frame.SelectedCaseValue.Value);
        var (conditionExpression, evaluatedValue) = ExtractBranchEvaluation(frame);
        var skippedFlowIds = frame.Output.HasValue
            && frame.Output.Value.ValueKind == JsonValueKind.Object
            && frame.Output.Value.TryGetProperty("skippedFlowIds", out var skipped)
            && skipped.ValueKind == JsonValueKind.Array
                ? skipped.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString())
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Cast<string>()
                    .Distinct(StringComparer.Ordinal)
                    .ToArray()
                : Array.Empty<string>();
        AppendEventLocked(buffer, "branch.selected", frame.EndedAt ?? frame.StartedAt, frame.ObjectId, frame.OutgoingFlowId, new
        {
            conditionExpression,
            evaluatedValue,
            selectedCaseLabel = label,
            selectedCaseValue = frame.SelectedCaseValue.Value,
            skippedFlowIds,
        });
    }

    private static void PublishLoopEventsLocked(RunBuffer buffer, MicroflowTraceFrameDto frame)
    {
        if (!frame.LoopIteration.HasValue || frame.LoopIteration.Value.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var loop = frame.LoopIteration.Value;
        var index = ReadInt(loop, "index") ?? ReadInt(loop, "iterationIndex");
        var total = ReadInt(loop, "total") ?? ReadInt(loop, "totalIterations");
        var iteratorName = ReadString(loop, "iteratorVariableName") ?? ReadString(loop, "iteratorName");
        var iteratorValuePreview = ReadString(loop, "iteratorValuePreview");
        if (index.HasValue)
        {
            AppendEventLocked(buffer, "loop.iteration.started", frame.StartedAt, frame.ObjectId, payload: new
            {
                index = index.Value,
                total,
                iteratorName,
                iteratorValuePreview,
            });
            AppendEventLocked(buffer, "loop.iteration.completed", frame.EndedAt ?? frame.StartedAt, frame.ObjectId, payload: new
            {
                index = index.Value,
                total,
                iteratorName,
                iteratorValuePreview,
            });
        }

        var message = frame.Message ?? string.Empty;
        if (message.Contains("break", StringComparison.OrdinalIgnoreCase))
        {
            AppendEventLocked(buffer, "loop.break", frame.EndedAt ?? frame.StartedAt, frame.ObjectId, payload: new { message });
        }
        if (message.Contains("continue", StringComparison.OrdinalIgnoreCase))
        {
            AppendEventLocked(buffer, "loop.continue", frame.EndedAt ?? frame.StartedAt, frame.ObjectId, payload: new { message });
        }
    }

    private static void PublishGatewayEventsLocked(RunBuffer buffer, MicroflowTraceFrameDto frame)
    {
        if (!frame.Output.HasValue || frame.Output.Value.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (!frame.Output.Value.TryGetProperty("branchTrace", out var branchTrace) || branchTrace.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var total = 0;
        var completed = 0;
        var skipped = 0;
        var failed = 0;
        foreach (var branch in branchTrace.EnumerateArray())
        {
            if (branch.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var flowId = ReadString(branch, "flowId") ?? ReadString(branch, "branchId");
            var status = ReadString(branch, "status") ?? "completed";
            var selected = ReadBool(branch, "selected") ?? false;
            if (string.IsNullOrWhiteSpace(flowId))
            {
                continue;
            }

            total++;
            if (string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase))
            {
                failed++;
            }
            else if (string.Equals(status, "skipped", StringComparison.OrdinalIgnoreCase))
            {
                skipped++;
            }
            else
            {
                completed++;
            }

            if (selected)
            {
                AppendEventLocked(buffer, "branch.selected", frame.EndedAt ?? frame.StartedAt, frame.ObjectId, flowId, new
                {
                    selectedCaseLabel = "selected",
                    selectedCaseValue = frame.SelectedCaseValue,
                    skippedFlowIds = branchTrace.EnumerateArray()
                        .Where(item =>
                            item.ValueKind == JsonValueKind.Object
                            && (ReadBool(item, "selected") ?? false) == false
                            && string.Equals(ReadString(item, "status") ?? "skipped", "skipped", StringComparison.OrdinalIgnoreCase))
                        .Select(item => ReadString(item, "flowId") ?? ReadString(item, "branchId"))
                        .Where(item => !string.IsNullOrWhiteSpace(item))
                        .Cast<string>()
                        .Distinct(StringComparer.Ordinal)
                        .ToArray()
                });
            }
            else if (string.Equals(status, "skipped", StringComparison.OrdinalIgnoreCase))
            {
                AppendEventLocked(buffer, "branch.skipped", frame.EndedAt ?? frame.StartedAt, frame.ObjectId, flowId, new { reason = "branchTrace" });
            }

            AppendEventLocked(buffer, "gateway.branch.started", frame.StartedAt, frame.ObjectId, flowId, new
            {
                status = "running",
                selected,
            });
            AppendEventLocked(buffer, "gateway.branch.completed", frame.EndedAt ?? frame.StartedAt, frame.ObjectId, flowId, new
            {
                status,
                selected,
            });
        }

        AppendEventLocked(buffer, "gateway.merge.completed", frame.EndedAt ?? frame.StartedAt, frame.ObjectId, payload: new
        {
            totalBranches = total,
            completedBranches = completed,
            skippedBranches = skipped,
            failedBranches = failed,
            mergeResultPreview = CompactPreview(frame.Output.Value)
        });
        if (failed > 0)
        {
            AppendEventLocked(buffer, "gateway.merge.failed", frame.EndedAt ?? frame.StartedAt, frame.ObjectId, payload: new
            {
                totalBranches = total,
                completedBranches = completed,
                skippedBranches = skipped,
                failedBranches = failed,
                mergeResultPreview = CompactPreview(frame.Output.Value)
            });
        }
    }

    private static void AppendEdgeVisited(RunBuffer buffer, string? flowId, DateTimeOffset timestamp)
    {
        if (string.IsNullOrWhiteSpace(flowId))
        {
            return;
        }

        AppendEventLocked(buffer, "edge.visited", timestamp, flowId: flowId, payload: new { status = "visited" });
    }

    private static string NormalizeNodeStatus(string status)
    {
        return status switch
        {
            "success" => "succeeded",
            "failed" => "failed",
            "running" => "running",
            "skipped" => "skipped",
            _ => status
        };
    }

    private static void AppendEventLocked(
        RunBuffer buffer,
        string type,
        DateTimeOffset timestamp,
        string? objectId = null,
        string? flowId = null,
        object? payload = null)
    {
        var next = new MicroflowRuntimeWsEvent
        {
            EventId = Guid.NewGuid().ToString("N"),
            RunId = buffer.RunId,
            Sequence = ++buffer.Sequence,
            Type = type,
            Timestamp = timestamp,
            ObjectId = objectId,
            FlowId = flowId,
            Payload = payload
        };
        buffer.Events.Add(next);
        if (buffer.Events.Count > MaxEventsPerRun)
        {
            buffer.Events.RemoveRange(0, buffer.Events.Count - MaxEventsPerRun);
        }

        ApplyOverlayLocked(buffer, next);
    }

    private static void ApplyOverlayLocked(RunBuffer buffer, MicroflowRuntimeWsEvent evt)
    {
        switch (evt.Type)
        {
            case "run.started":
                buffer.Status = "running";
                return;
            case "run.completed":
                buffer.Status = "completed";
                buffer.Result = ReadPayloadJson(evt.Payload, "result");
                return;
            case "run.failed":
                buffer.Status = "failed";
                buffer.Result = null;
                return;
            case "heartbeat":
                return;
        }

        if (!string.IsNullOrWhiteSpace(evt.ObjectId))
        {
            buffer.CurrentObjectId = evt.ObjectId;
        }

        if (!string.IsNullOrWhiteSpace(evt.ObjectId))
        {
            var objectId = evt.ObjectId!;
            var current = buffer.NodeOverlays.TryGetValue(objectId, out var hit)
                ? hit
                : new MicroflowRuntimeOverlayNode { ObjectId = objectId };
            current = evt.Type switch
            {
                "node.started" => current with { Status = "running" },
                "node.inputResolved" => current with { InputSummary = ReadPayloadList(evt.Payload, "inputSummary") },
                "node.outputProduced" => current with
                {
                    OutputSummary = ReadPayloadList(evt.Payload, "outputSummary"),
                    VariableDeltaSummary = ReadPayloadList(evt.Payload, "variableDeltaSummary"),
                },
                "node.completed" => current with
                {
                    Status = "succeeded",
                    DurationMs = ReadPayloadLong(evt.Payload, "durationMs")
                },
                "node.failed" => current with
                {
                    Status = "failed",
                    DurationMs = ReadPayloadLong(evt.Payload, "durationMs"),
                    Error = ReadPayload(evt.Payload, "error")
                },
                "branch.selected" => current with
                {
                    SelectedCaseLabel = ReadPayloadString(evt.Payload, "selectedCaseLabel"),
                    SelectedCaseValue = ReadPayloadJson(evt.Payload, "selectedCaseValue")
                },
                "loop.iteration.started" => current with { LoopIteration = evt.Payload },
                "loop.break" => current with { LoopIteration = MergeControl(ReadPayload(evt.Payload, "message"), "break") },
                "loop.continue" => current with { LoopIteration = MergeControl(ReadPayload(evt.Payload, "message"), "continue") },
                "gateway.merge.completed" => current with { GatewaySummary = evt.Payload },
                "gateway.merge.failed" => current with
                {
                    GatewaySummary = evt.Payload,
                    Status = "failed",
                },
                _ => current
            };
            buffer.NodeOverlays[objectId] = current;
        }

        if (!string.IsNullOrWhiteSpace(evt.FlowId)
            && evt.Type is "edge.visited" or "edge.running" or "edge.failed" or "edge.errorHandlerVisited")
        {
            var flowId = evt.FlowId!;
            buffer.FlowOverlays[flowId] = new MicroflowRuntimeOverlayFlow
            {
                FlowId = flowId,
                Status = evt.Type switch
                {
                    "edge.running" => "running",
                    "edge.failed" => "failed",
                    "edge.errorHandlerVisited" => "errorHandlerVisited",
                    _ => "visited"
                },
                VisitedAt = evt.Timestamp
            };
            return;
        }

        if (!string.IsNullOrWhiteSpace(evt.FlowId) && evt.Type is "branch.selected" or "branch.skipped")
        {
            var flowId = evt.FlowId!;
            buffer.FlowOverlays[flowId] = new MicroflowRuntimeOverlayFlow
            {
                FlowId = flowId,
                Status = evt.Type == "branch.selected" ? "selectedCase" : "skipped",
                SelectedCaseValue = ReadPayloadJson(evt.Payload, "selectedCaseValue"),
                VisitedAt = evt.Timestamp
            };
        }
    }

    private static object MergeControl(object? message, string control)
        => new { control, message = message?.ToString() };

    private static string? ReadString(JsonElement element, string key)
        => element.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static (string? ConditionExpression, JsonElement? EvaluatedValue) ExtractBranchEvaluation(MicroflowTraceFrameDto frame)
    {
        if (!frame.EvaluatedExpressions.HasValue || frame.EvaluatedExpressions.Value.ValueKind != JsonValueKind.Array)
        {
            return (null, null);
        }

        string? conditionExpression = null;
        JsonElement? evaluatedValue = null;
        foreach (var item in frame.EvaluatedExpressions.Value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            conditionExpression ??= ReadString(item, "conditionExpression")
                ?? ReadString(item, "expression")
                ?? ReadString(item, "condition");
            evaluatedValue ??= ReadElement(item, "evaluatedValue")
                ?? ReadElement(item, "result")
                ?? ReadElement(item, "value");

            if (conditionExpression is not null && evaluatedValue.HasValue)
            {
                break;
            }
        }

        if (!evaluatedValue.HasValue && frame.SelectedCaseValue.HasValue)
        {
            evaluatedValue = frame.SelectedCaseValue.Value;
        }
        return (conditionExpression, evaluatedValue);
    }

    private static bool? ReadBool(JsonElement element, string key)
    {
        if (!element.TryGetProperty(key, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }

    private static int? ReadInt(JsonElement element, string key)
        => element.TryGetProperty(key, out var value) && value.TryGetInt32(out var parsed) ? parsed : null;

    private static JsonElement? ReadElement(JsonElement element, string key)
        => element.TryGetProperty(key, out var value) ? value.Clone() : null;

    private static string ReadDataType(JsonElement variable)
    {
        if (!variable.TryGetProperty("type", out var type))
        {
            return "unknown";
        }

        if (type.ValueKind == JsonValueKind.Object && type.TryGetProperty("kind", out var kind) && kind.ValueKind == JsonValueKind.String)
        {
            return kind.GetString() ?? "unknown";
        }

        return CompactPreview(type);
    }

    private static string CompactPreview(JsonElement? value)
    {
        if (!value.HasValue)
        {
            return string.Empty;
        }

        var json = value.Value.GetRawText();
        if (json.Length <= 96)
        {
            return json;
        }

        return $"{json[..93]}...";
    }

    private static IReadOnlyList<object>? ReadPayloadList(object? payload, string key)
    {
        if (payload is null)
        {
            return null;
        }

        if (payload is JsonElement element && element.ValueKind == JsonValueKind.Object && element.TryGetProperty(key, out var listElement) && listElement.ValueKind == JsonValueKind.Array)
        {
            return JsonSerializer.Deserialize<List<object>>(listElement.GetRawText(), JsonOptions);
        }

        var json = JsonSerializer.SerializeToElement(payload, JsonOptions);
        if (json.ValueKind == JsonValueKind.Object && json.TryGetProperty(key, out var list) && list.ValueKind == JsonValueKind.Array)
        {
            return JsonSerializer.Deserialize<List<object>>(list.GetRawText(), JsonOptions);
        }

        return null;
    }

    private static object? ReadPayload(object? payload, string key)
    {
        if (payload is null)
        {
            return null;
        }

        var json = JsonSerializer.SerializeToElement(payload, JsonOptions);
        if (json.ValueKind == JsonValueKind.Object && json.TryGetProperty(key, out var value))
        {
            return JsonSerializer.Deserialize<object>(value.GetRawText(), JsonOptions);
        }

        return null;
    }

    private static string? ReadPayloadString(object? payload, string key)
        => ReadPayload(payload, key)?.ToString();

    private static long? ReadPayloadLong(object? payload, string key)
    {
        var value = ReadPayload(payload, key);
        if (value is null)
        {
            return null;
        }

        if (value is JsonElement element && element.TryGetInt64(out var longValue))
        {
            return longValue;
        }

        if (long.TryParse(value.ToString(), out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static JsonElement? ReadPayloadJson(object? payload, string key)
    {
        var value = ReadPayload(payload, key);
        if (value is null)
        {
            return null;
        }

        return JsonSerializer.SerializeToElement(value, JsonOptions);
    }

    private sealed class RunBuffer
    {
        public RunBuffer(string runId)
        {
            RunId = runId;
        }

        public object SyncRoot { get; } = new();

        public string RunId { get; }

        public bool Started { get; set; }

        public string Status { get; set; } = "idle";

        public long Sequence { get; set; }

        public DateTimeOffset? StartedAt { get; set; }

        public JsonElement? Result { get; set; }

        public string? CurrentObjectId { get; set; }

        public List<MicroflowRuntimeWsEvent> Events { get; } = [];

        public Dictionary<string, MicroflowRuntimeOverlayNode> NodeOverlays { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, MicroflowRuntimeOverlayFlow> FlowOverlays { get; } = new(StringComparer.Ordinal);
    }
}
