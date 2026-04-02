using Atlas.Application.LogicFlow.Flows.Repositories;
using Atlas.Application.Resilience;
using Atlas.Domain.LogicFlow.Flows;

namespace Atlas.Infrastructure.Resilience;

public sealed class CompensationService : ICompensationService
{
    private readonly INodeRunRepository _nodeRuns;

    public CompensationService(INodeRunRepository nodeRuns)
    {
        _nodeRuns = nodeRuns;
    }

    public async Task RegisterCompensationAsync(
        long executionId,
        string nodeKey,
        string compensationDataJson,
        CancellationToken cancellationToken)
    {
        var run = await _nodeRuns
            .GetByExecutionIdAndNodeKeyAsync(executionId, nodeKey, cancellationToken)
            .ConfigureAwait(false);
        if (run is null)
            throw new InvalidOperationException($"Node run not found for execution {executionId} and node '{nodeKey}'.");

        run.CompensationDataJson = string.IsNullOrWhiteSpace(compensationDataJson) ? "{}" : compensationDataJson;
        await _nodeRuns.UpdateAsync(run, cancellationToken).ConfigureAwait(false);
    }

    public async Task<CompensationResult> ExecuteCompensationsAsync(
        long executionId,
        CancellationToken cancellationToken)
    {
        var all = await _nodeRuns.GetByExecutionIdAsync(executionId, cancellationToken).ConfigureAwait(false);
        var candidates = all
            .Where(x => !string.IsNullOrWhiteSpace(x.CompensationDataJson) && !x.IsCompensated)
            .OrderByDescending(x => x.Id)
            .ToList();

        var steps = new List<CompensationStepResult>();
        var allOk = true;
        foreach (var run in candidates)
        {
            try
            {
                run.IsCompensated = true;
                var updated = await _nodeRuns.UpdateAsync(run, cancellationToken).ConfigureAwait(false);
                steps.Add(new CompensationStepResult(run.NodeKey, updated, updated ? null : "Update returned no rows."));
                if (!updated)
                    allOk = false;
            }
            catch (Exception ex)
            {
                allOk = false;
                steps.Add(new CompensationStepResult(run.NodeKey, false, ex.Message));
            }
        }

        return new CompensationResult(allOk, steps);
    }
}
