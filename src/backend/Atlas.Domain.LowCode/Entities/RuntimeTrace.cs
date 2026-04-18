using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.LowCode.Entities;

/// <summary>
/// 运行时 trace（M13 S13-2）。一次 dispatch 对应一条 RuntimeTrace + 多条 RuntimeSpan。
/// 6 维查询：traceId / 页面 / 组件 / 时间范围 / 错误类型 / 用户 / 租户（tenantId 由 TenantEntity 自动隔离）。
/// </summary>
public sealed class RuntimeTrace : TenantEntity
{
#pragma warning disable CS8618
    public RuntimeTrace() : base(TenantId.Empty)
    {
        TraceId = string.Empty;
        AppId = string.Empty;
        Status = "running";
    }
#pragma warning restore CS8618

    public RuntimeTrace(TenantId tenantId, long id, string traceId, string appId, string? pageId, string? componentId, string? eventName, long userId)
        : base(tenantId)
    {
        Id = id;
        TraceId = traceId;
        AppId = appId;
        PageId = pageId;
        ComponentId = componentId;
        EventName = eventName;
        UserId = userId;
        Status = "running";
        StartedAt = DateTimeOffset.UtcNow;
    }

    [SugarColumn(Length = 64, IsNullable = false)]
    public string TraceId { get; private set; }

    [SugarColumn(Length = 128, IsNullable = false)]
    public string AppId { get; private set; }

    [SugarColumn(Length = 64, IsNullable = true)]
    public string? PageId { get; private set; }

    [SugarColumn(Length = 64, IsNullable = true)]
    public string? ComponentId { get; private set; }

    [SugarColumn(Length = 64, IsNullable = true)]
    public string? EventName { get; private set; }

    public long UserId { get; private set; }

    /// <summary>running / success / failed。</summary>
    [SugarColumn(Length = 32, IsNullable = false)]
    public string Status { get; private set; }

    /// <summary>错误类型（M13 6 维检索之一），失败时填充。</summary>
    [SugarColumn(Length = 64, IsNullable = true)]
    public string? ErrorKind { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? EndedAt { get; private set; }

    public void MarkSuccess()
    {
        Status = "success";
        EndedAt = DateTimeOffset.UtcNow;
    }

    public void MarkFailed(string errorKind)
    {
        Status = "failed";
        ErrorKind = errorKind;
        EndedAt = DateTimeOffset.UtcNow;
    }
}

/// <summary>
/// 运行时 span（M13 S13-2）。属于某个 RuntimeTrace。
/// </summary>
public sealed class RuntimeSpan : TenantEntity
{
#pragma warning disable CS8618
    public RuntimeSpan() : base(TenantId.Empty)
    {
        SpanId = string.Empty;
        TraceId = string.Empty;
        Name = string.Empty;
        Status = "ok";
    }
#pragma warning restore CS8618

    public RuntimeSpan(TenantId tenantId, long id, string spanId, string? parentSpanId, string traceId, string name)
        : base(tenantId)
    {
        Id = id;
        SpanId = spanId;
        ParentSpanId = parentSpanId;
        TraceId = traceId;
        Name = name;
        Status = "ok";
        StartedAt = DateTimeOffset.UtcNow;
    }

    [SugarColumn(Length = 64, IsNullable = false)]
    public string SpanId { get; private set; }

    [SugarColumn(Length = 64, IsNullable = true)]
    public string? ParentSpanId { get; private set; }

    [SugarColumn(Length = 64, IsNullable = false)]
    public string TraceId { get; private set; }

    /// <summary>span 名（dispatcher.start / action.invoke / workflow.invoke / chatflow.stream / asset.upload / state.patch / error）。</summary>
    [SugarColumn(Length = 128, IsNullable = false)]
    public string Name { get; private set; }

    /// <summary>ok / error。</summary>
    [SugarColumn(Length = 32, IsNullable = false)]
    public string Status { get; private set; }

    /// <summary>属性 JSON（脱敏后）。</summary>
    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? AttributesJson { get; private set; }

    [SugarColumn(Length = 2000, IsNullable = true)]
    public string? ErrorMessage { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? EndedAt { get; private set; }

    public void Finish(bool ok, string? attributesJson, string? errorMessage)
    {
        Status = ok ? "ok" : "error";
        AttributesJson = attributesJson;
        ErrorMessage = errorMessage;
        EndedAt = DateTimeOffset.UtcNow;
    }
}
