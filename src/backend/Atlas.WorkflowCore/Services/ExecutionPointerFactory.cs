using System;
using System.Collections.Generic;
using System.Linq;
using Atlas.WorkflowCore.Abstractions;
using Atlas.WorkflowCore.Models;

namespace Atlas.WorkflowCore.Services;

/// <summary>
/// 执行指针工厂实现
/// </summary>
public class ExecutionPointerFactory : IExecutionPointerFactory
{
    public ExecutionPointer BuildGenesisPointer(WorkflowDefinition def)
    {
        var firstStep = def.Steps.FindById(0);
        return new ExecutionPointer
        {
            Id = GenerateId(),
            StepId = 0,
            Active = true,
            Status = PointerStatus.Pending,
            StepName = firstStep?.Name ?? "Genesis"
        };
    }

    public ExecutionPointer BuildNextPointer(WorkflowDefinition def, ExecutionPointer pointer, IStepOutcome outcomeTarget)
    {
        var nextId = GenerateId();
        var nextStep = def.Steps.FindById(outcomeTarget.NextStep);
        return new ExecutionPointer
        {
            Id = nextId,
            PredecessorId = pointer.Id,
            StepId = outcomeTarget.NextStep,
            Active = true,
            ContextItem = pointer.ContextItem,
            Status = PointerStatus.Pending,
            StepName = nextStep?.Name ?? "Unknown",
            Scope = new List<string>(pointer.Scope)
        };
    }

    public ExecutionPointer BuildChildPointer(WorkflowDefinition def, ExecutionPointer pointer, int childDefinitionId, object branch)
    {
        var childPointerId = GenerateId();
        var childScope = new List<string>(pointer.Scope);
        childScope.Insert(0, pointer.Id);
        pointer.Children.Add(childPointerId);

        var childStep = def.Steps.FindById(childDefinitionId);
        return new ExecutionPointer
        {
            Id = childPointerId,
            PredecessorId = pointer.Id,
            StepId = childDefinitionId,
            Active = true,
            ContextItem = branch,
            Status = PointerStatus.Pending,
            StepName = childStep?.Name ?? "Unknown",
            Scope = new List<string>(childScope)
        };
    }

    public ExecutionPointer BuildCompensationPointer(WorkflowDefinition def, ExecutionPointer pointer, ExecutionPointer exceptionPointer, int compensationStepId)
    {
        var nextId = GenerateId();
        var compensationStep = def.Steps.FindById(compensationStepId);
        return new ExecutionPointer
        {
            Id = nextId,
            PredecessorId = exceptionPointer.Id,
            StepId = compensationStepId,
            Active = true,
            ContextItem = pointer.ContextItem,
            Status = PointerStatus.Pending,
            StepName = compensationStep?.Name ?? "Unknown",
            Scope = new List<string>(pointer.Scope)
        };
    }

    private string GenerateId()
    {
        return Guid.NewGuid().ToString();
    }
}
