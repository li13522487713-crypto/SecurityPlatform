using Atlas.Application.LogicFlow.Flows.Models;

namespace Atlas.Application.LogicFlow.Flows.Abstractions;

public interface IDagScheduler
{
    IReadOnlyList<string> GetReadySet(PhysicalDagPlan plan, ISet<string> completedNodeKeys);

    IReadOnlyList<string> TopologicalSort(PhysicalDagPlan plan);
}
