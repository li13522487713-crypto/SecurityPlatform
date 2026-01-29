using Atlas.WorkflowCore.Abstractions;
using Atlas.WorkflowCore.Builders;
using Atlas.WorkflowCore.Models;

namespace Atlas.WorkflowCore.Services;

public class WorkflowRegistry : IWorkflowRegistry
{
    private readonly Dictionary<string, Dictionary<int, WorkflowDefinition>> _workflows = new();

    public void RegisterWorkflow(IWorkflow workflow)
    {
        RegisterWorkflow<object>(workflow);
    }

    public void RegisterWorkflow(WorkflowDefinition definition)
    {
        if (!_workflows.ContainsKey(definition.Id))
        {
            _workflows[definition.Id] = new Dictionary<int, WorkflowDefinition>();
        }

        _workflows[definition.Id][definition.Version] = definition;
    }

    public void RegisterWorkflow<TData>(IWorkflow<TData> workflow) where TData : new()
    {
        var builder = new WorkflowBuilder<TData>();
        workflow.Build(builder);
        var definition = builder.Build(workflow.Id, workflow.Version);
        RegisterWorkflow(definition);
    }

    public WorkflowDefinition? GetDefinition(string workflowId, int? version = null)
    {
        if (!_workflows.ContainsKey(workflowId))
        {
            return null;
        }

        var versions = _workflows[workflowId];

        if (version.HasValue)
        {
            return versions.TryGetValue(version.Value, out var def) ? def : null;
        }

        return versions.Values.OrderByDescending(x => x.Version).FirstOrDefault();
    }

    public bool IsRegistered(string workflowId, int version)
    {
        return _workflows.ContainsKey(workflowId) && _workflows[workflowId].ContainsKey(version);
    }

    public void DeregisterWorkflow(string workflowId, int version)
    {
        if (_workflows.ContainsKey(workflowId))
        {
            _workflows[workflowId].Remove(version);
            if (_workflows[workflowId].Count == 0)
            {
                _workflows.Remove(workflowId);
            }
        }
    }

    public IEnumerable<WorkflowDefinition> GetAllDefinitions()
    {
        return _workflows.Values.SelectMany(x => x.Values);
    }
}
