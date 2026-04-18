using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.LowCode.Entities;

/// <summary>
/// 低代码会话（M11 S11-2）。
/// 多会话切换；与 chatflow 绑定。
/// </summary>
public sealed class LowCodeSession : TenantEntity
{
#pragma warning disable CS8618
    public LowCodeSession()
        : base(TenantId.Empty)
    {
        SessionId = string.Empty;
    }
#pragma warning restore CS8618

    public LowCodeSession(TenantId tenantId, long id, string sessionId, long userId, string? title)
        : base(tenantId)
    {
        Id = id;
        SessionId = sessionId;
        UserId = userId;
        Title = title;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
        Status = "active";
    }

    [SugarColumn(Length = 64, IsNullable = false)]
    public string SessionId { get; private set; }

    public long UserId { get; private set; }

    [SugarColumn(Length = 200, IsNullable = true)]
    public string? Title { get; private set; }

    public bool Pinned { get; private set; }

    /// <summary>active / archived。</summary>
    [SugarColumn(Length = 32, IsNullable = false)]
    public string Status { get; private set; }

    /// <summary>当前是否为 paused 态（M11 中断/恢复/插入）。</summary>
    public bool Paused { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Touch() { UpdatedAt = DateTimeOffset.UtcNow; }
    public void Pin(bool pinned) { Pinned = pinned; UpdatedAt = DateTimeOffset.UtcNow; }
    public void Archive(bool archived) { Status = archived ? "archived" : "active"; UpdatedAt = DateTimeOffset.UtcNow; }
    public void Pause() { Paused = true; UpdatedAt = DateTimeOffset.UtcNow; }
    public void Resume() { Paused = false; UpdatedAt = DateTimeOffset.UtcNow; }
}

/// <summary>
/// 低代码消息日志聚合（M11 S11-5）。
/// 跨域聚合：chatflow message / workflow trace / agent 调用 / 工具调用 / dispatch 事件。
/// </summary>
public sealed class LowCodeMessageLogEntry : TenantEntity
{
#pragma warning disable CS8618
    public LowCodeMessageLogEntry()
        : base(TenantId.Empty)
    {
        EntryId = string.Empty;
        Source = string.Empty;
        Kind = string.Empty;
    }
#pragma warning restore CS8618

    public LowCodeMessageLogEntry(TenantId tenantId, long id, string entryId, string source, string kind, string? sessionId, string? workflowId, string? agentId, string? traceId, string payloadJson)
        : base(tenantId)
    {
        Id = id;
        EntryId = entryId;
        Source = source;
        Kind = kind;
        SessionId = sessionId;
        WorkflowId = workflowId;
        AgentId = agentId;
        TraceId = traceId;
        PayloadJson = payloadJson;
        OccurredAt = DateTimeOffset.UtcNow;
    }

    [SugarColumn(Length = 64, IsNullable = false)]
    public string EntryId { get; private set; }

    /// <summary>来源域：chatflow / workflow / agent / tool / dispatch。</summary>
    [SugarColumn(Length = 32, IsNullable = false)]
    public string Source { get; private set; }

    /// <summary>Kind：message/tool_call/error/final/start/end/...</summary>
    [SugarColumn(Length = 64, IsNullable = false)]
    public string Kind { get; private set; }

    [SugarColumn(Length = 64, IsNullable = true)]
    public string? SessionId { get; private set; }

    [SugarColumn(Length = 128, IsNullable = true)]
    public string? WorkflowId { get; private set; }

    [SugarColumn(Length = 128, IsNullable = true)]
    public string? AgentId { get; private set; }

    [SugarColumn(Length = 64, IsNullable = true)]
    public string? TraceId { get; private set; }

    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? PayloadJson { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }
}
