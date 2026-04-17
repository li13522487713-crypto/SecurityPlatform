using System.Runtime.CompilerServices;
using System.Text.Json;
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
    private readonly IRuntimeMessageLogService _messageLog;
    private readonly IIdGeneratorAccessor _idGen;
    private readonly IAuditWriter _auditWriter;

    public RuntimeChatflowService(ILowCodeSessionRepository sessionRepo, IRuntimeMessageLogService messageLog, IIdGeneratorAccessor idGen, IAuditWriter auditWriter)
    {
        _sessionRepo = sessionRepo;
        _messageLog = messageLog;
        _idGen = idGen;
        _auditWriter = auditWriter;
    }

    public async IAsyncEnumerable<string> StreamSseAsync(TenantId tenantId, long currentUserId, RuntimeChatflowInvokeRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var sessionId = await EnsureSessionAsync(tenantId, currentUserId, request.SessionId, cancellationToken);
        await _messageLog.RecordAsync(tenantId, "chatflow", "user_input", sessionId, null, null, null, JsonSerializer.Serialize(new { text = request.Input }), cancellationToken);

        var seq = 0;
        // M11 默认实现：走简化 mock pipeline（tool_call → 多帧 message → final）；
        // 真实模型对接由 docs/coze-api-gap.md 流式接入计划逐步替换为 IDagWorkflowExecutionService.StreamRunAsync。
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

        await _messageLog.RecordAsync(tenantId, "chatflow", "final", sessionId, null, null, null, JsonSerializer.Serialize(outputs), cancellationToken);
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.runtime.chatflow.invoke", "success", $"sess:{sessionId}:cf:{request.ChatflowId}", null, null), cancellationToken);
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

        var seq = 0;
        // M11 简化：恢复后给一帧 message + final，提示恢复成功；真实续流在对接模型时实现。
        seq++;
        yield return Sse(new { kind = "message", content = "[resumed]", markdown = false, seq });
        await Task.Delay(10, cancellationToken);
        seq++;
        yield return Sse(new { kind = "final", outputs = new { resumed = true }, seq });

        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.runtime.chatflow.resume", "success", $"sess:{sessionId}", null, null), cancellationToken);
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
