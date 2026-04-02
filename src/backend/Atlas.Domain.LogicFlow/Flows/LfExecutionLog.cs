using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.LogicFlow.Flows;

[SugarTable("lf_execution_log")]
public sealed class LfExecutionLog : TenantEntity
{
    public LfExecutionLog() : base(default) { }

    public LfExecutionLog(
        TenantId tenantId,
        long flowExecutionId,
        string? nodeKey,
        string level,
        string message,
        string? structuredDataJson,
        DateTime timestamp,
        string? traceId,
        string? spanId)
        : base(tenantId)
    {
        FlowExecutionId = flowExecutionId;
        NodeKey = nodeKey;
        Level = level;
        Message = message;
        StructuredDataJson = structuredDataJson;
        Timestamp = timestamp;
        TraceId = traceId;
        SpanId = spanId;
    }

    [SugarColumn(IsPrimaryKey = true)]
    public new long Id { get => base.Id; set => SetId(value); }

    public long FlowExecutionId { get; set; }

    [SugarColumn(Length = 100, IsNullable = true)]
    public string? NodeKey { get; set; }

    [SugarColumn(Length = 20)]
    public string Level { get; set; } = string.Empty;

    [SugarColumn(ColumnDataType = "text")]
    public string Message { get; set; } = string.Empty;

    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? StructuredDataJson { get; set; }

    public DateTime Timestamp { get; set; }

    [SugarColumn(Length = 64, IsNullable = true)]
    public string? TraceId { get; set; }

    [SugarColumn(Length = 32, IsNullable = true)]
    public string? SpanId { get; set; }
}
