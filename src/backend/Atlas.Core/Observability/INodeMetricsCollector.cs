namespace Atlas.Core.Observability;

public interface INodeMetricsCollector
{
    void RecordExecution(string nodeTypeKey, long durationMs, bool succeeded);
    NodeMetrics GetMetrics(string nodeTypeKey);
    IReadOnlyDictionary<string, NodeMetrics> GetAllMetrics();
}

public sealed record NodeMetrics(
    string NodeTypeKey,
    long TotalExecutions,
    long SuccessCount,
    long FailureCount,
    double AvgDurationMs,
    long MaxDurationMs,
    long MinDurationMs,
    DateTime LastExecutedAt);
