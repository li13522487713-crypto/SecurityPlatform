namespace Atlas.Core.Observability;

public interface IExecutionLogger
{
    Task LogAsync(ExecutionLogEntry entry, CancellationToken ct);
    Task<IReadOnlyList<ExecutionLogEntry>> QueryAsync(ExecutionLogQuery query, CancellationToken ct);
}

public sealed record ExecutionLogEntry(
    long FlowExecutionId,
    string? NodeKey,
    string Level,
    string Message,
    string? StructuredDataJson,
    DateTime Timestamp,
    string? TraceId,
    string? SpanId);

public sealed record ExecutionLogQuery(
    long? FlowExecutionId,
    string? NodeKey,
    string? Level,
    DateTime? From,
    DateTime? To,
    int PageIndex = 1,
    int PageSize = 50);
