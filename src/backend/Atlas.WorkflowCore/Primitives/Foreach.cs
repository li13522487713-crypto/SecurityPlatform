using System.Collections;
using Atlas.WorkflowCore.Abstractions;
using Atlas.WorkflowCore.Models;

namespace Atlas.WorkflowCore.Primitives;

public class Foreach : ContainerStepBody
{
    public IEnumerable? Collection { get; set; }
    public bool RunParallel { get; set; } = true;

    public override ExecutionResult Run(IStepExecutionContext context)
    {
        if (context.PersistenceData == null)
        {
            if (Collection == null)
            {
                return ExecutionResult.Next();
            }

            var values = Collection.Cast<object>().ToList();
            if (!values.Any())
            {
                return ExecutionResult.Next();
            }

            if (RunParallel)
            {
                return ExecutionResult.Branch(new List<object>(values), new IteratorPersistenceData { ChildrenActive = true });
            }
            else
            {
                return ExecutionResult.Branch(new List<object>(new object[] { values.ElementAt(0) }), new IteratorPersistenceData { ChildrenActive = true });
            }
        }

        if (context.PersistenceData is IteratorPersistenceData persistenceData && persistenceData.ChildrenActive)
        {
            if (context.Workflow.IsBranchComplete(context.ExecutionPointer.Id))
            {
                if (!RunParallel)
                {
                    if (Collection == null)
                    {
                        return ExecutionResult.Next();
                    }

                    var values = Collection.Cast<object>().ToList();
                    persistenceData.Index++;
                    if (persistenceData.Index < values.Count)
                    {
                        return ExecutionResult.Branch(new List<object>(new object[] { values.ElementAt(persistenceData.Index) }), persistenceData);
                    }
                }

                return ExecutionResult.Next();
            }

            return ExecutionResult.Persist(persistenceData);
        }

        if (context.PersistenceData is ControlPersistenceData controlPersistenceData && controlPersistenceData.ChildrenActive)
        {
            if (context.Workflow.IsBranchComplete(context.ExecutionPointer.Id))
            {
                return ExecutionResult.Next();
            }
        }

        return ExecutionResult.Persist(context.PersistenceData);
    }
}
