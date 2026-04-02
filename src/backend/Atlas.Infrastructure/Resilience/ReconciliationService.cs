using Atlas.Application.Resilience;
using Atlas.Domain.LogicFlow.Flows;
using SqlSugar;

namespace Atlas.Infrastructure.Resilience;

public sealed class ReconciliationService : IReconciliationService
{
    private const int BatchSize = 500;

    private readonly ISqlSugarClient _db;

    public ReconciliationService(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<ReconciliationReport> ReconcileAsync(string scope, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(scope)
            && !scope.Equals("logic-flow", StringComparison.OrdinalIgnoreCase))
        {
            return new ReconciliationReport(scope, 0, 0, Array.Empty<ReconciliationMismatch>());
        }

        var executions = await _db.Queryable<FlowExecution>()
            .OrderBy(x => x.Id)
            .Take(BatchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (executions.Count == 0)
            return new ReconciliationReport(
                string.IsNullOrWhiteSpace(scope) ? "logic-flow" : scope,
                0,
                0,
                Array.Empty<ReconciliationMismatch>());

        var ids = executions.Select(e => e.Id).ToArray();
        var runs = await _db.Queryable<NodeRun>()
            .Where(x => ids.Contains(x.FlowExecutionId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var byExecution = runs.GroupBy(x => x.FlowExecutionId).ToDictionary(g => g.Key, g => g.ToList());
        var mismatches = new List<ReconciliationMismatch>();

        foreach (var execution in executions)
        {
            if (!byExecution.TryGetValue(execution.Id, out var nodes))
                nodes = new List<NodeRun>();

            if (execution.Status != ExecutionStatus.Completed)
                continue;

            var bad = nodes.Any(n =>
                n.Status is NodeRunStatus.Failed
                    or NodeRunStatus.Pending
                    or NodeRunStatus.Running
                    or NodeRunStatus.WaitingForRetry);

            if (bad)
            {
                mismatches.Add(new ReconciliationMismatch(
                    nameof(FlowExecution),
                    execution.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    nameof(ExecutionStatus.Completed),
                    $"NodeRuns contain non-terminal failure/pending states (count={nodes.Count})."));
            }
        }

        var resolvedScope = string.IsNullOrWhiteSpace(scope) ? "logic-flow" : scope;
        return new ReconciliationReport(resolvedScope, executions.Count, mismatches.Count, mismatches);
    }
}
