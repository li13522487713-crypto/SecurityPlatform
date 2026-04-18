using System.Runtime.CompilerServices;
using System.Text.Json;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Application.Audit.Abstractions;
using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Application.LowCode.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Audit.Entities;
using Atlas.Domain.LowCode.Entities;

namespace Atlas.Infrastructure.Services.LowCode;

/// <summary>
/// 多会话管理（M11 S11-2）。
/// </summary>
public sealed class RuntimeSessionService : IRuntimeSessionService
{
    private readonly ILowCodeSessionRepository _repo;
    private readonly IIdGeneratorAccessor _idGen;
    private readonly IAuditWriter _auditWriter;

    public RuntimeSessionService(ILowCodeSessionRepository repo, IIdGeneratorAccessor idGen, IAuditWriter auditWriter)
    {
        _repo = repo;
        _idGen = idGen;
        _auditWriter = auditWriter;
    }

    public async Task<IReadOnlyList<RuntimeSessionInfo>> ListAsync(TenantId tenantId, long currentUserId, CancellationToken cancellationToken)
    {
        var list = await _repo.ListByUserAsync(tenantId, currentUserId, cancellationToken);
        return list.Select(s => new RuntimeSessionInfo(s.SessionId, s.Title, s.Pinned, s.Status == "archived", s.UpdatedAt)).ToList();
    }

    public async Task<string> CreateAsync(TenantId tenantId, long currentUserId, RuntimeSessionCreateRequest request, CancellationToken cancellationToken)
    {
        var sessionId = $"sess_{_idGen.NextId()}";
        var entity = new LowCodeSession(tenantId, _idGen.NextId(), sessionId, currentUserId, request.Title);
        await _repo.InsertAsync(entity, cancellationToken);
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.runtime.session.create", "success", $"sess:{sessionId}", null, null), cancellationToken);
        return sessionId;
    }

    public async Task ClearAsync(TenantId tenantId, long currentUserId, string sessionId, CancellationToken cancellationToken)
    {
        await _repo.ClearMessagesAsync(tenantId, sessionId, cancellationToken);
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.runtime.session.clear", "success", $"sess:{sessionId}", null, null), cancellationToken);
    }

    public async Task PinAsync(TenantId tenantId, long currentUserId, string sessionId, RuntimeSessionPinRequest request, CancellationToken cancellationToken)
    {
        var s = await _repo.FindBySessionIdAsync(tenantId, sessionId, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, $"会话不存在：{sessionId}");
        s.Pin(request.Pinned);
        await _repo.UpdateAsync(s, cancellationToken);
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.runtime.session.pin", "success", $"sess:{sessionId}:pinned:{request.Pinned}", null, null), cancellationToken);
    }

    public async Task ArchiveAsync(TenantId tenantId, long currentUserId, string sessionId, RuntimeSessionArchiveRequest request, CancellationToken cancellationToken)
    {
        var s = await _repo.FindBySessionIdAsync(tenantId, sessionId, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, $"会话不存在：{sessionId}");
        s.Archive(request.Archived);
        await _repo.UpdateAsync(s, cancellationToken);
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.runtime.session.archive", "success", $"sess:{sessionId}:archived:{request.Archived}", null, null), cancellationToken);
    }
}

/// <summary>
/// Chatflow SSE 服务（M11 S11-1 / S11-3 / S11-4）。
///
/// M11 阶段：实现 SSE 协议的客户端契约 + 中断/恢复/插入的会话状态管理；
/// 真实模型流式由现有 Coze 兼容层逐步对接（docs/coze-api-gap.md 中 chatflow stream fallback → OK）。
/// 本实现产出可被 RuntimeChatflowsController 转 text/event-stream 直接返回的字符串流。
/// </summary>
public sealed class RuntimeChatflowService : IRuntimeChatflowService
{
    private readonly ILowCodeSessionRepository _sessionRepo;
    private readonly ILowCodeMessageLogRepository _messageLogRepo;
    private readonly IRuntimeMessageLogService _messageLog;
    private readonly IDagWorkflowExecutionService _engine;
    private readonly IIdGeneratorAccessor _idGen;
    private readonly IAuditWriter _auditWriter;

    public RuntimeChatflowService(ILowCodeSessionRepository sessionRepo, ILowCodeMessageLogRepository messageLogRepo, IRuntimeMessageLogService messageLog, IDagWorkflowExecutionService engine, IIdGeneratorAccessor idGen, IAuditWriter auditWriter)
    {
        _sessionRepo = sessionRepo;
        _messageLogRepo = messageLogRepo;
        _messageLog = messageLog;
        _engine = engine;
        _idGen = idGen;
        _auditWriter = auditWriter;
    }

    public async IAsyncEnumerable<string> StreamSseAsync(TenantId tenantId, long currentUserId, RuntimeChatflowInvokeRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var sessionId = await EnsureSessionAsync(tenantId, currentUserId, request.SessionId, cancellationToken);
        // user_input：把 chatflowId + 完整 input + context 写入 payload，便于 ResumeSseAsync 续流时按最近 user_input 重发
        await _messageLog.RecordAsync(tenantId, "chatflow", "user_input", sessionId, request.ChatflowId, null, null,
            JsonSerializer.Serialize(new { text = request.Input, chatflowId = request.ChatflowId, context = request.Context }), cancellationToken);

        // M11 收尾：若 chatflowId 是 long（DAG 工作流 ID），桥接到 IDagWorkflowExecutionService.StreamRunAsync 真实流式；
        // 否则走 mock pipeline（用于无后端 chatflow 时的本地调试）。
        var seq = 0;
        var bridged = false;
        var hadFinal = false;
        if (long.TryParse(request.ChatflowId, out var workflowId))
        {
            var inputsJson = JsonSerializer.Serialize(new
            {
                input = request.Input,
                sessionId,
                context = request.Context
            });
            DagWorkflowRunRequest runReq = new(inputsJson, source: "lowcode-chatflow");
            IAsyncEnumerable<SseEvent>? stream = null;
            string? startError = null;
            try
            {
                stream = _engine.StreamRunAsync(tenantId, workflowId, currentUserId, runReq, cancellationToken);
            }
            catch (Exception ex)
            {
                startError = ex.Message;
            }
            if (startError is not null)
            {
                seq++;
                yield return Sse(new { kind = "error", message = $"启动 chatflow 引擎失败：{startError}", recoverable = false, seq });
                await _messageLog.RecordAsync(tenantId, "chatflow", "error", sessionId, request.ChatflowId, null, null, JsonSerializer.Serialize(new { message = startError }), cancellationToken);
            }
            if (stream is not null)
            {
                bridged = true;
                await foreach (var evt in stream.WithCancellation(cancellationToken))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var (kind, payload) = MapEngineEventToChunk(evt, ++seq);
                    yield return payload;
                    LowCodeOtelInstrumentation.ChatflowStreamChunk.Add(1,
                        new KeyValuePair<string, object?>("tenant.id", tenantId.Value.ToString()),
                        new KeyValuePair<string, object?>("lowcode.chatflow_id", request.ChatflowId),
                        new KeyValuePair<string, object?>("chunk.kind", kind));
                    if (kind == "final") hadFinal = true;
                }
            }
        }

        if (!bridged)
        {
            // 回退：mock pipeline（与 M11 初版一致），便于无 LLM 时仍可演示。
            seq++;
            yield return Sse(new { kind = "tool_call", toolName = "compose", args = new { input = request.Input }, seq });
            var tokens = SplitTokens(request.Input);
            foreach (var token in tokens)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(30, cancellationToken);
                seq++;
                yield return Sse(new { kind = "message", content = token, markdown = true, seq });
            }
            seq++;
            var outputs = new { final = string.Concat(tokens), inputEcho = request.Input };
            yield return Sse(new { kind = "final", outputs, seq });
            hadFinal = true;
        }

        if (!hadFinal)
        {
            seq++;
            yield return Sse(new { kind = "final", outputs = new { final = "" }, seq });
        }

        await _messageLog.RecordAsync(tenantId, "chatflow", "final", sessionId, request.ChatflowId, null, null, JsonSerializer.Serialize(new { bridged }), cancellationToken);
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.runtime.chatflow.invoke", "success", $"sess:{sessionId}:cf:{request.ChatflowId}:bridged:{bridged}", null, null), cancellationToken);
    }

    /// <summary>
    /// 把 IDagWorkflowExecutionService 的 SseEvent (event/data) 协议转为前端 ChatChunk 4 类（tool_call/message/error/final）。
    /// </summary>
    private static (string Kind, string Payload) MapEngineEventToChunk(SseEvent evt, int seq)
    {
        var ev = evt.Event?.ToLowerInvariant() ?? "message";
        return ev switch
        {
            "tool_call" or "function_call" => ("tool_call", Sse(new { kind = "tool_call", toolName = "tool", args = SafeParse(evt.Data), seq })),
            "error" => ("error", Sse(new { kind = "error", message = evt.Data, recoverable = false, seq })),
            "final" or "complete" or "done" => ("final", Sse(new { kind = "final", outputs = SafeParseObject(evt.Data), seq })),
            _ => ("message", Sse(new { kind = "message", content = evt.Data, markdown = true, seq })),
        };
    }

    private static object? SafeParse(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<JsonElement>(json); }
        catch { return new { raw = json }; }
    }

    private static object SafeParseObject(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new { final = "" };
        try { return JsonSerializer.Deserialize<JsonElement>(json); }
        catch { return new { final = json }; }
    }

    public async Task PauseAsync(TenantId tenantId, long currentUserId, string sessionId, CancellationToken cancellationToken)
    {
        var s = await _sessionRepo.FindBySessionIdAsync(tenantId, sessionId, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, $"会话不存在：{sessionId}");
        s.Pause();
        await _sessionRepo.UpdateAsync(s, cancellationToken);
        await _messageLog.RecordAsync(tenantId, "chatflow", "pause", sessionId, null, null, null, "{}", cancellationToken);
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.runtime.chatflow.pause", "success", $"sess:{sessionId}", null, null), cancellationToken);
    }

    public async IAsyncEnumerable<string> ResumeSseAsync(TenantId tenantId, long currentUserId, string sessionId, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var s = await _sessionRepo.FindBySessionIdAsync(tenantId, sessionId, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, $"会话不存在：{sessionId}");
        s.Resume();
        await _sessionRepo.UpdateAsync(s, cancellationToken);
        await _messageLog.RecordAsync(tenantId, "chatflow", "resume", sessionId, null, null, null, "{}", cancellationToken);

        // 真实续流：从消息日志找该会话最近一条 user_input 提取 chatflowId+input，
        // 拼出 RuntimeChatflowInvokeRequest 走完整 StreamSseAsync 流（同时把 user_inject 也合并）。
        var entries = await _messageLogRepo.QueryAsync(tenantId, sessionId, null, null, null, null, 1, 200, cancellationToken);
        var lastInput = entries.OrderByDescending(e => e.OccurredAt).FirstOrDefault(e => e.Source == "chatflow" && e.Kind == "user_input");
        var injects = entries.Where(e => e.Source == "chatflow" && e.Kind == "user_inject" && lastInput is not null && e.OccurredAt >= lastInput.OccurredAt)
                             .OrderBy(e => e.OccurredAt).ToList();

        if (lastInput is null)
        {
            // 无可续流上下文 → 仅发出一帧 final 表示恢复完成
            yield return Sse(new { kind = "final", outputs = new { resumed = true, reason = "no-prior-input" }, seq = 1 });
            await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.runtime.chatflow.resume", "success", $"sess:{sessionId}:no-input", null, null), cancellationToken);
            yield break;
        }

        // 解析 payload 中的 chatflowId + input
        string? chatflowId = lastInput.WorkflowId;
        string input = "";
        Dictionary<string, JsonElement>? context = null;
        try
        {
            var payload = JsonSerializer.Deserialize<JsonElement>(lastInput.PayloadJson ?? "{}");
            if (payload.TryGetProperty("chatflowId", out var cf) && cf.ValueKind == JsonValueKind.String)
                chatflowId = cf.GetString();
            if (payload.TryGetProperty("text", out var tx) && tx.ValueKind == JsonValueKind.String)
                input = tx.GetString() ?? "";
            if (payload.TryGetProperty("context", out var ctx) && ctx.ValueKind == JsonValueKind.Object)
            {
                context = new Dictionary<string, JsonElement>();
                foreach (var p in ctx.EnumerateObject()) context[p.Name] = p.Value;
            }
        }
        catch (JsonException) { /* 容错：无效 payload 仍继续以 chatflowId/empty input 续流 */ }

        // 把 inject 的内容追加到 input 中（保持顺序）
        foreach (var inj in injects)
        {
            try
            {
                var p = JsonSerializer.Deserialize<JsonElement>(inj.PayloadJson ?? "{}");
                if (p.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String)
                {
                    input += "\n" + m.GetString();
                }
            }
            catch (JsonException) { /* skip */ }
        }

        if (string.IsNullOrEmpty(chatflowId))
        {
            yield return Sse(new { kind = "final", outputs = new { resumed = true, reason = "no-chatflow-id" }, seq = 1 });
            await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.runtime.chatflow.resume", "success", $"sess:{sessionId}:no-cf", null, null), cancellationToken);
            yield break;
        }

        var resumeReq = new RuntimeChatflowInvokeRequest(chatflowId, sessionId, input, context);
        await foreach (var chunk in StreamSseAsync(tenantId, currentUserId, resumeReq, cancellationToken))
        {
            yield return chunk;
        }
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.runtime.chatflow.resume", "success", $"sess:{sessionId}:cf:{chatflowId}:injects:{injects.Count}", null, null), cancellationToken);
    }

    public async Task InjectAsync(TenantId tenantId, long currentUserId, string sessionId, RuntimeChatflowInjectRequest request, CancellationToken cancellationToken)
    {
        var s = await _sessionRepo.FindBySessionIdAsync(tenantId, sessionId, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, $"会话不存在：{sessionId}");
        s.Touch();
        await _sessionRepo.UpdateAsync(s, cancellationToken);
        await _messageLog.RecordAsync(tenantId, "chatflow", "user_inject", sessionId, null, null, null, JsonSerializer.Serialize(new { message = request.Message }), cancellationToken);
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.runtime.chatflow.inject", "success", $"sess:{sessionId}", null, null), cancellationToken);
    }

    private async Task<string> EnsureSessionAsync(TenantId tenantId, long currentUserId, string? sessionId, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            var existing = await _sessionRepo.FindBySessionIdAsync(tenantId, sessionId, cancellationToken);
            if (existing is not null) return sessionId;
        }
        var newId = $"sess_{_idGen.NextId()}";
        var entity = new LowCodeSession(tenantId, _idGen.NextId(), newId, currentUserId, title: null);
        await _sessionRepo.InsertAsync(entity, cancellationToken);
        return newId;
    }

    private static IReadOnlyList<string> SplitTokens(string input)
    {
        // 简化：按字符分；真实接入 LLM 流式 tokens 由 IDagWorkflowExecutionService.StreamRunAsync 提供。
        if (string.IsNullOrEmpty(input)) return new[] { "..." };
        var tokens = new List<string>(input.Length);
        foreach (var c in input) tokens.Add(c.ToString());
        return tokens;
    }

    private static string Sse(object payload)
    {
        return $"data: {JsonSerializer.Serialize(payload)}\n\n";
    }
}

public sealed class RuntimeMessageLogService : IRuntimeMessageLogService
{
    private readonly ILowCodeMessageLogRepository _repo;
    private readonly IIdGeneratorAccessor _idGen;

    public RuntimeMessageLogService(ILowCodeMessageLogRepository repo, IIdGeneratorAccessor idGen)
    {
        _repo = repo;
        _idGen = idGen;
    }

    public async Task<IReadOnlyList<RuntimeMessageLogEntryDto>> QueryAsync(TenantId tenantId, RuntimeMessageLogQuery query, CancellationToken cancellationToken)
    {
        var pageIndex = query.PageIndex ?? 1;
        var pageSize = query.PageSize ?? 100;
        var list = await _repo.QueryAsync(tenantId, query.SessionId, query.WorkflowId, query.AgentId, query.From, query.To, pageIndex, pageSize, cancellationToken);
        return list.Select(e => new RuntimeMessageLogEntryDto(
            e.EntryId,
            e.Source,
            e.Kind,
            e.SessionId,
            e.WorkflowId,
            e.AgentId,
            e.TraceId,
            string.IsNullOrWhiteSpace(e.PayloadJson) ? (JsonElement?)null : JsonSerializer.Deserialize<JsonElement>(e.PayloadJson!),
            e.OccurredAt
        )).ToList();
    }

    public async Task RecordAsync(TenantId tenantId, string source, string kind, string? sessionId, string? workflowId, string? agentId, string? traceId, string payloadJson, CancellationToken cancellationToken)
    {
        var entryId = $"mle_{_idGen.NextId()}";
        var entry = new LowCodeMessageLogEntry(tenantId, _idGen.NextId(), entryId, source, kind, sessionId, workflowId, agentId, traceId, payloadJson);
        await _repo.InsertAsync(entry, cancellationToken);
    }
}
