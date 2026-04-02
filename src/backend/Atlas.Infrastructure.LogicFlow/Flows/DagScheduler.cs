using Atlas.Application.LogicFlow.Flows.Abstractions;
using Atlas.Application.LogicFlow.Flows.Models;

namespace Atlas.Infrastructure.LogicFlow.Flows;

public sealed class DagScheduler : IDagScheduler
{
    public IReadOnlyList<string> GetReadySet(PhysicalDagPlan plan, ISet<string> completedNodeKeys)
    {
        var completed = new HashSet<string>(completedNodeKeys, StringComparer.Ordinal);
        var ready = new List<string>();
        foreach (var node in plan.Nodes)
        {
            if (completed.Contains(node.NodeKey))
                continue;

            var allDepsDone = node.Dependencies.Count == 0
                || node.Dependencies.All(d => completed.Contains(d));
            if (allDepsDone)
                ready.Add(node.NodeKey);
        }

        return ready;
    }

    public IReadOnlyList<string> TopologicalSort(PhysicalDagPlan plan)
    {
        var nodeKeys = plan.Nodes.Select(n => n.NodeKey).ToHashSet(StringComparer.Ordinal);
        var inDegree = nodeKeys.ToDictionary(k => k, _ => 0, StringComparer.Ordinal);
        var adj = nodeKeys.ToDictionary(k => k, _ => new List<string>(), StringComparer.Ordinal);

        foreach (var edge in plan.Edges)
        {
            if (!nodeKeys.Contains(edge.SourceNodeKey) || !nodeKeys.Contains(edge.TargetNodeKey))
                continue;
            adj[edge.SourceNodeKey].Add(edge.TargetNodeKey);
            inDegree[edge.TargetNodeKey]++;
        }

        var queue = new Queue<string>();
        foreach (var kv in inDegree)
        {
            if (kv.Value == 0)
                queue.Enqueue(kv.Key);
        }

        var result = new List<string>();
        while (queue.Count > 0)
        {
            var u = queue.Dequeue();
            result.Add(u);
            foreach (var v in adj[u])
            {
                inDegree[v]--;
                if (inDegree[v] == 0)
                    queue.Enqueue(v);
            }
        }

        return result;
    }
}
