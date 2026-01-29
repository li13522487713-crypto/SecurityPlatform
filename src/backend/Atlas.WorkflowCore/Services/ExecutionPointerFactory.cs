using Atlas.WorkflowCore.Abstractions;
using Atlas.WorkflowCore.Models;

namespace Atlas.WorkflowCore.Services;

/// <summary>
/// 执行指针工厂实现
/// </summary>
public class ExecutionPointerFactory : IExecutionPointerFactory
{
    public ExecutionPointer BuildGenesisPointer(WorkflowStep step)
    {
        return new ExecutionPointer
        {
            Id = Guid.NewGuid().ToString(),
            StepId = step.Id,
            StepName = step.Name,
            Active = true,
            Status = PointerStatus.Pending,
            StartTime = null,
            EndTime = null,
            SleepUntil = null,
            PersistenceData = null,
            EventName = null,
            EventKey = null,
            EventData = null,
            EventPublished = false,
            RetryCount = 0,
            Children = new List<string>(),
            Scope = new List<string>()
        };
    }

    public ExecutionPointer BuildNextPointer(WorkflowStep step, ExecutionPointer parentPointer)
    {
        return new ExecutionPointer
        {
            Id = Guid.NewGuid().ToString(),
            StepId = step.Id,
            StepName = step.Name,
            Active = true,
            Status = PointerStatus.Pending,
            StartTime = null,
            EndTime = null,
            SleepUntil = null,
            PersistenceData = null,
            EventName = null,
            EventKey = null,
            EventData = null,
            EventPublished = false,
            RetryCount = 0,
            Children = new List<string>(),
            Scope = new List<string>(parentPointer.Scope),
            PredecessorId = parentPointer.Id
        };
    }

    public ExecutionPointer BuildChildPointer(WorkflowStep step, ExecutionPointer parentPointer, string scope)
    {
        var childScope = new List<string>(parentPointer.Scope) { scope };

        var pointer = new ExecutionPointer
        {
            Id = Guid.NewGuid().ToString(),
            StepId = step.Id,
            StepName = step.Name,
            Active = true,
            Status = PointerStatus.Pending,
            StartTime = null,
            EndTime = null,
            SleepUntil = null,
            PersistenceData = null,
            EventName = null,
            EventKey = null,
            EventData = null,
            EventPublished = false,
            RetryCount = 0,
            Children = new List<string>(),
            Scope = childScope,
            PredecessorId = parentPointer.Id
        };

        parentPointer.Children.Add(pointer.Id);

        return pointer;
    }

    public ExecutionPointer BuildCompensationPointer(WorkflowStep step, ExecutionPointer parentPointer)
    {
        return new ExecutionPointer
        {
            Id = Guid.NewGuid().ToString(),
            StepId = step.Id,
            StepName = step.Name,
            Active = true,
            Status = PointerStatus.Pending,
            StartTime = null,
            EndTime = null,
            SleepUntil = null,
            PersistenceData = null,
            EventName = null,
            EventKey = null,
            EventData = null,
            EventPublished = false,
            RetryCount = 0,
            Children = new List<string>(),
            Scope = new List<string>(parentPointer.Scope),
            PredecessorId = parentPointer.Id
        };
    }
}
