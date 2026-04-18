using System.Diagnostics;
using System.Text.Json;
using Atlas.Application.Audit.Abstractions;
using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Application.LowCode.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Audit.Entities;
using Atlas.Domain.LowCode.Entities;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services.LowCode;

public sealed class RuntimeTraceService : IRuntimeTraceService
{
    private readonly IRuntimeTraceRepository _repo;
    private readonly IRuntimeMessageLogService _messageLog;
    private readonly IIdGeneratorAccessor _idGen;
    private readonly ISensitiveMaskingService _masking;

    public RuntimeTraceService(IRuntimeTraceRepository repo, IRuntimeMessageLogService messageLog, IIdGeneratorAccessor idGen, ISensitiveMaskingService masking)
    {
        _repo = repo;
        _messageLog = messageLog;
        _idGen = idGen;
        _masking = masking;
    }

    public async Task<RuntimeTraceDto?> GetTraceAsync(TenantId tenantId, string traceId, CancellationToken cancellationToken)
    {
        var trace = await _repo.FindByTraceIdAsync(tenantId, traceId, cancellationToken);
        if (trace is null) return null;
        var spans = await _repo.ListSpansByTraceAsync(tenantId, traceId, cancellationToken);
        return ToDto(trace, spans);
    }

    public async Task<IReadOnlyList<RuntimeTraceDto>> QueryAsync(TenantId tenantId, RuntimeTraceQuery query, CancellationToken cancellationToken)
    {
        var pageIndex = query.PageIndex ?? 1;
        var pageSize = query.PageSize ?? 50;
        long? userId = long.TryParse(query.UserId, out var uid) ? uid : null;
        if (!string.IsNullOrWhiteSpace(query.TraceId))
        {
            var t = await _repo.FindByTraceIdAsync(tenantId, query.TraceId!, cancellationToken);
            if (t is null) return Array.Empty<RuntimeTraceDto>();
            var spans = await _repo.ListSpansByTraceAsync(tenantId, query.TraceId!, cancellationToken);
            return new[] { ToDto(t, spans) };
        }
        var traces = await _repo.QueryTracesAsync(tenantId, query.AppId, query.PageId, query.ComponentId, query.From, query.To, query.ErrorType, userId, pageIndex, pageSize, cancellationToken);
        // 列表模式不展开 spans，以减负
        return traces.Select(t => ToDto(t, Array.Empty<RuntimeSpan>())).ToList();
    }

    public async Task<string> StartTraceAsync(TenantId tenantId, long currentUserId, string appId, string? pageId, string? componentId, string? eventName, CancellationToken cancellationToken)
    {
        // 与 OTel Activity 统一 traceId：若上层已有 Activity 则继承，否则启动一个 root activity。
        var activity = LowCodeOtelInstrumentation.ActivitySource.StartActivity("lowcode.dispatch.start", ActivityKind.Server);
        if (activity is not null)
        {
            activity.SetTag("tenant.id", tenantId.Value.ToString());
            activity.SetTag("lowcode.app_id", appId);
            if (pageId is not null) activity.SetTag("lowcode.page_id", pageId);
            if (componentId is not null) activity.SetTag("lowcode.component_id", componentId);
            if (eventName is not null) activity.SetTag("lowcode.event", eventName);
        }
        var traceId = activity?.TraceId.ToString() ?? Activity.Current?.TraceId.ToString() ?? $"trc_{_idGen.NextId():x16}";
        var entity = new RuntimeTrace(tenantId, _idGen.NextId(), traceId, appId, pageId, componentId, eventName, currentUserId);
        await _repo.InsertTraceAsync(entity, cancellationToken);
        // activity 由调用方自行 Dispose（dispatch 结束时由 FinishTraceAsync 完成）；此处只记录起点。
        activity?.Dispose();
        return traceId;
    }

    public async Task FinishTraceAsync(TenantId tenantId, string traceId, bool success, string? errorKind, CancellationToken cancellationToken)
    {
        var entity = await _repo.FindByTraceIdAsync(tenantId, traceId, cancellationToken);
        if (entity is null) return;
        if (success) entity.MarkSuccess();
        else entity.MarkFailed(errorKind ?? "unknown");
        await _repo.UpdateTraceAsync(entity, cancellationToken);

        // OTel 指标：dispatch 延迟 + 错误计数
        if (entity.EndedAt.HasValue)
        {
            var latencyMs = (entity.EndedAt.Value - entity.StartedAt).TotalMilliseconds;
            LowCodeOtelInstrumentation.DispatchLatencyMs.Record(latencyMs,
                new KeyValuePair<string, object?>("tenant.id", tenantId.Value.ToString()),
                new KeyValuePair<string, object?>("lowcode.app_id", entity.AppId),
                new KeyValuePair<string, object?>("status", entity.Status));
        }
        if (!success)
        {
            LowCodeOtelInstrumentation.ErrorCount.Add(1,
                new KeyValuePair<string, object?>("tenant.id", tenantId.Value.ToString()),
                new KeyValuePair<string, object?>("source", "dispatch"),
                new KeyValuePair<string, object?>("error.kind", errorKind ?? "unknown"));
        }
    }

    public async Task<string> AddSpanAsync(TenantId tenantId, string traceId, string? parentSpanId, string name, string? attributesJson, bool ok, string? errorMessage, CancellationToken cancellationToken)
    {
        var spanId = $"sp_{_idGen.NextId():x16}";
        var maskedAttrs = _masking.Mask(attributesJson);
        var maskedErr = _masking.Mask(errorMessage);
        var span = new RuntimeSpan(tenantId, _idGen.NextId(), spanId, parentSpanId, traceId, name);
        span.Finish(ok, maskedAttrs, maskedErr);
        await _repo.InsertSpansBatchAsync(new[] { span }, cancellationToken);

        // OTel Activity（短生命周期），便于 OTel exporter 看到 span 拓扑（即使持久化在 SQLite）。
        using var activity = LowCodeOtelInstrumentation.ActivitySource.StartActivity(name, ActivityKind.Internal);
        activity?.SetTag("lowcode.trace_id", traceId);
        activity?.SetTag("lowcode.span_id", spanId);
        if (parentSpanId is not null) activity?.SetTag("lowcode.parent_span_id", parentSpanId);
        activity?.SetTag("lowcode.status", ok ? "ok" : "error");
        if (!ok)
        {
            activity?.SetStatus(ActivityStatusCode.Error, errorMessage ?? "error");
            LowCodeOtelInstrumentation.ErrorCount.Add(1,
                new KeyValuePair<string, object?>("tenant.id", tenantId.Value.ToString()),
                new KeyValuePair<string, object?>("source", "span"),
                new KeyValuePair<string, object?>("span.name", name));
        }

        // 若是 workflow span，单独记录 workflow latency（按 attributesJson 含 status 标签做最佳努力解析）。
        if (name.StartsWith("action.call_workflow", StringComparison.OrdinalIgnoreCase) || name.StartsWith("workflow.invoke", StringComparison.OrdinalIgnoreCase))
        {
            // span 内不暴露具体 workflowId，使用 attributesJson 中含 kind 字段时填入；否则不打 tag。
            LowCodeOtelInstrumentation.WorkflowLatencyMs.Record(
                Math.Max(0, (DateTimeOffset.UtcNow - span.StartedAt).TotalMilliseconds),
                new KeyValuePair<string, object?>("tenant.id", tenantId.Value.ToString()),
                new KeyValuePair<string, object?>("status", ok ? "success" : "failed"));
        }

        return spanId;
    }

    private static RuntimeTraceDto ToDto(RuntimeTrace t, IReadOnlyList<RuntimeSpan> spans)
    {
        var spanDtos = spans.Select(s => new RuntimeSpanDto(
            s.SpanId, s.ParentSpanId, s.Name, s.Status,
            string.IsNullOrWhiteSpace(s.AttributesJson) ? (JsonElement?)null : JsonSerializer.Deserialize<JsonElement>(s.AttributesJson!),
            s.ErrorMessage, s.StartedAt, s.EndedAt
        )).ToList();
        return new RuntimeTraceDto(t.TraceId, t.AppId, t.PageId, t.ComponentId, t.EventName, t.Status, t.ErrorKind, t.UserId.ToString(), t.StartedAt, t.EndedAt, spanDtos);
    }
}

/// <summary>
/// dispatch 执行器（M13 S13-1）。统一处理事件 → 解析 ActionDto → 调用相应 Adapter / Service → 收集 statePatches。
///
/// M13 阶段：内置实现 set_variable / navigate / open_external_link / show_toast / update_component（纯前端语义直接合成 patch），
/// 把 call_workflow / call_chatflow / call_plugin 委托给后端 RuntimeWorkflowExecutor / RuntimeChatflowService。
/// </summary>
public sealed class DispatchExecutor : IDispatchExecutor
{
    private readonly IRuntimeTraceService _trace;
    private readonly IRuntimeWorkflowExecutor _workflow;
    private readonly IRuntimeMessageLogService _messageLog;
    private readonly IAuditWriter _auditWriter;
    private readonly ILogger<DispatchExecutor> _logger;

    public DispatchExecutor(IRuntimeTraceService trace, IRuntimeWorkflowExecutor workflow, IRuntimeMessageLogService messageLog, IAuditWriter auditWriter, ILogger<DispatchExecutor> logger)
    {
        _trace = trace;
        _workflow = workflow;
        _messageLog = messageLog;
        _auditWriter = auditWriter;
        _logger = logger;
    }

    public async Task<DispatchResponse> DispatchAsync(TenantId tenantId, long currentUserId, DispatchRequest request, CancellationToken cancellationToken)
    {
        var traceId = await _trace.StartTraceAsync(tenantId, currentUserId, request.AppId, request.PageId, request.ComponentId, request.EventName, cancellationToken);
        var patches = new List<DispatchStatePatchDto>();
        var outputs = new Dictionary<string, JsonElement>();
        var messages = new List<DispatchMessageDto>();
        var errors = new List<DispatchErrorDto>();
        await _trace.AddSpanAsync(tenantId, traceId, null, "dispatcher.start", JsonSerializer.Serialize(new { request.AppId, request.PageId, actionsCount = request.Actions.Length }), ok: true, errorMessage: null, cancellationToken);

        try
        {
            foreach (var action in request.Actions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await ExecuteActionAsync(tenantId, currentUserId, traceId, action, request, patches, outputs, messages, errors, cancellationToken);
            }
            await _trace.FinishTraceAsync(tenantId, traceId, success: errors.Count == 0, errorKind: errors.Count == 0 ? null : errors[0].Kind, cancellationToken);
            await _messageLog.RecordAsync(tenantId, "dispatch", "exit", null, null, null, traceId, JsonSerializer.Serialize(new { patches = patches.Count, errors = errors.Count }), cancellationToken);
        }
        catch (Exception ex)
        {
            errors.Add(new DispatchErrorDto("dispatcher_exception", ex.Message, ex.StackTrace));
            await _trace.FinishTraceAsync(tenantId, traceId, success: false, errorKind: "dispatcher_exception", cancellationToken);
            _logger.LogError(ex, "dispatch failed traceId={TraceId}", traceId);
        }

        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.runtime.dispatch", errors.Count == 0 ? "success" : "failed", $"trace:{traceId}:actions:{request.Actions.Length}", null, null), cancellationToken);
        return new DispatchResponse(traceId, outputs, patches, messages, errors);
    }

    private async Task ExecuteActionAsync(TenantId tenantId, long currentUserId, string traceId, DispatchActionDto action, DispatchRequest request, List<DispatchStatePatchDto> patches, Dictionary<string, JsonElement> outputs, List<DispatchMessageDto> messages, List<DispatchErrorDto> errors, CancellationToken cancellationToken)
    {
        var spanName = $"action.{action.Kind}";
        var attrs = JsonSerializer.Serialize(new { kind = action.Kind, id = action.Id });
        try
        {
            switch (action.Kind)
            {
                case "set_variable":
                {
                    if (action.Payload is JsonElement p)
                    {
                        var path = p.TryGetProperty("targetPath", out var tp) ? tp.GetString() : null;
                        var scope = p.TryGetProperty("scopeRoot", out var sr) ? sr.GetString() : "page";
                        if (!string.IsNullOrEmpty(path) && (scope is "page" or "app"))
                        {
                            var valueElem = p.TryGetProperty("value", out var ve) ? ve : default;
                            patches.Add(new DispatchStatePatchDto(scope!, path!, "set", valueElem.ValueKind == JsonValueKind.Undefined ? null : valueElem, null));
                        }
                        else if (scope is not "page" and not "app")
                        {
                            errors.Add(new DispatchErrorDto("scope_violation", $"set_variable 不允许写入 scope={scope}", null));
                        }
                    }
                    break;
                }
                case "show_toast":
                {
                    if (action.Payload is JsonElement p)
                    {
                        var text = p.TryGetProperty("text", out var t) ? t.GetString() ?? string.Empty : string.Empty;
                        var kind = p.TryGetProperty("toastType", out var k) ? k.GetString() ?? "info" : "info";
                        messages.Add(new DispatchMessageDto(kind, text));
                    }
                    break;
                }
                case "navigate":
                case "open_external_link":
                case "update_component":
                {
                    // 这些动作的具体行为依赖前端运行时（navigation / window.open / component.props 更新），
                    // 后端只产出"指令型 outputs"由前端消费。
                    if (action.Payload is JsonElement p)
                    {
                        outputs[action.Kind] = p;
                    }
                    break;
                }
                case "call_workflow":
                {
                    if (action.Payload is JsonElement p)
                    {
                        var workflowId = p.TryGetProperty("workflowId", out var w) ? w.GetString() ?? string.Empty : string.Empty;
                        Dictionary<string, JsonElement>? inputs = null;
                        if (p.TryGetProperty("inputs", out var inp) && inp.ValueKind == JsonValueKind.Object)
                        {
                            inputs = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(inp.GetRawText());
                        }
                        var wfReq = new RuntimeWorkflowInvokeRequest(workflowId, inputs, request.AppId, request.PageId, request.VersionId, request.ComponentId, null);
                        var r = await _workflow.InvokeAsync(tenantId, currentUserId, wfReq, cancellationToken);
                        if (r.Outputs is not null)
                        {
                            foreach (var (k, v) in r.Outputs) outputs[k] = v;
                        }
                        if (r.Patches is not null)
                        {
                            patches.AddRange(r.Patches.Select(p => new DispatchStatePatchDto(p.Scope, p.Path, p.Op, p.Value, p.ComponentId)));
                        }
                    }
                    break;
                }
                default:
                    // 未知 kind：交给前端处理（往 outputs 落字面 payload）
                    if (action.Payload is JsonElement other) outputs[$"unknown_{action.Kind}"] = other;
                    break;
            }
            await _trace.AddSpanAsync(tenantId, traceId, null, spanName, attrs, ok: true, errorMessage: null, cancellationToken);
        }
        catch (Exception ex)
        {
            errors.Add(new DispatchErrorDto(action.Kind, ex.Message, ex.StackTrace));
            await _trace.AddSpanAsync(tenantId, traceId, null, spanName, attrs, ok: false, errorMessage: ex.Message, cancellationToken);
            // onError 子链
            if (action.OnError is { Length: > 0 })
            {
                foreach (var sub in action.OnError)
                {
                    await ExecuteActionAsync(tenantId, currentUserId, traceId, sub, request, patches, outputs, messages, errors, cancellationToken);
                }
            }
        }
    }
}
