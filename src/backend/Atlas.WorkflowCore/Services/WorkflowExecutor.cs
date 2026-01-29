using System;
using System.Linq;
using Atlas.WorkflowCore.Abstractions;
using Atlas.WorkflowCore.Models;
using Microsoft.Extensions.Logging;

namespace Atlas.WorkflowCore.Services;

public class WorkflowExecutor : IWorkflowExecutor
{
    private readonly IWorkflowRegistry _registry;
    private readonly IServiceProvider _serviceProvider;
    private readonly IStepExecutor _stepExecutor;
    private readonly ILogger<WorkflowExecutor> _logger;

    public WorkflowExecutor(
        IWorkflowRegistry registry,
        IServiceProvider serviceProvider,
        IStepExecutor stepExecutor,
        ILogger<WorkflowExecutor> logger)
    {
        _registry = registry;
        _serviceProvider = serviceProvider;
        _stepExecutor = stepExecutor;
        _logger = logger;
    }

    public async Task<WorkflowExecutorResult> Execute(WorkflowInstance workflow, CancellationToken cancellationToken = default)
    {
        var result = new WorkflowExecutorResult();
        var def = _registry.GetDefinition(workflow.WorkflowDefinitionId, workflow.Version);
        if (def == null)
        {
            _logger.LogError("Workflow {WorkflowDefinitionId} version {Version} is not registered", workflow.WorkflowDefinitionId, workflow.Version);
            return result;
        }

        var exePointers = workflow.ExecutionPointers
            .Where(x => x.Active && (!x.SleepUntil.HasValue || x.SleepUntil < DateTime.UtcNow))
            .ToList();

        foreach (var pointer in exePointers)
        {
            if (!pointer.Active)
                continue;

            var step = def.Steps.FindById(pointer.StepId);
            if (step == null)
            {
                _logger.LogError("Unable to find step {StepId} in workflow definition", pointer.StepId);
                pointer.SleepUntil = DateTime.UtcNow.Add(TimeSpan.FromMinutes(1));
                result.Errors.Add(new ExecutionError
                {
                    WorkflowId = workflow.Id,
                    ExecutionPointerId = pointer.Id,
                    ErrorTime = DateTime.UtcNow,
                    Message = $"Unable to find step {pointer.StepId} in workflow definition"
                });
                continue;
            }

            try
            {
                if (!InitializeStep(workflow, step, result, def, pointer))
                    continue;

                await ExecuteStep(workflow, step, pointer, result, def, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Workflow {WorkflowId} raised error on step {StepId} Message: {Message}", workflow.Id, pointer.StepId, ex.Message);
                result.Errors.Add(new ExecutionError
                {
                    WorkflowId = workflow.Id,
                    ExecutionPointerId = pointer.Id,
                    ErrorTime = DateTime.UtcNow,
                    Message = ex.Message
                });
            }
        }

        ProcessAfterExecutionIteration(workflow, def, result);
        DetermineNextExecutionTime(workflow, def);

        return result;
    }

    private bool InitializeStep(WorkflowInstance workflow, WorkflowStep step, WorkflowExecutorResult wfResult, WorkflowDefinition def, ExecutionPointer pointer)
    {
        switch (step.InitForExecution(wfResult, def, workflow, pointer))
        {
            case ExecutionPipelineDirective.Defer:
                return false;
            case ExecutionPipelineDirective.EndWorkflow:
                workflow.Status = WorkflowStatus.Complete;
                workflow.CompleteTime = DateTime.UtcNow;
                return false;
        }

        if (pointer.Status != PointerStatus.Running)
        {
            pointer.Status = PointerStatus.Running;
        }

        if (!pointer.StartTime.HasValue)
        {
            pointer.StartTime = DateTime.UtcNow;
        }

        return true;
    }

    private async Task ExecuteStep(WorkflowInstance workflow, WorkflowStep step, ExecutionPointer pointer, WorkflowExecutorResult wfResult, WorkflowDefinition def, CancellationToken cancellationToken)
    {
        var context = new StepExecutionContext
        {
            Workflow = workflow,
            Step = step,
            PersistenceData = pointer.PersistenceData,
            ExecutionPointer = pointer,
            Item = pointer.ContextItem,
            CancellationToken = cancellationToken
        };

        _logger.LogDebug("Starting step {StepName} on workflow {WorkflowId}", step.Name, workflow.Id);

        var body = step.ConstructBody(_serviceProvider);
        if (body == null)
        {
            _logger.LogError("Unable to construct step body {BodyType}", step.BodyType.ToString());
            pointer.SleepUntil = DateTime.UtcNow.Add(TimeSpan.FromMinutes(1));
            wfResult.Errors.Add(new ExecutionError
            {
                WorkflowId = workflow.Id,
                ExecutionPointerId = pointer.Id,
                ErrorTime = DateTime.UtcNow,
                Message = $"Unable to construct step body {step.BodyType}"
            });
            return;
        }

        switch (step.BeforeExecute(wfResult, context, pointer, body))
        {
            case ExecutionPipelineDirective.Defer:
                return;
            case ExecutionPipelineDirective.EndWorkflow:
                workflow.Status = WorkflowStatus.Complete;
                workflow.CompleteTime = DateTime.UtcNow;
                return;
        }

        var executionResult = await _stepExecutor.ExecuteStep(context, body);

        ProcessExecutionResult(workflow, def, pointer, step, executionResult, wfResult);
        step.AfterExecute(wfResult, context, executionResult, pointer);
    }

    private void ProcessExecutionResult(WorkflowInstance workflow, WorkflowDefinition def, ExecutionPointer pointer, WorkflowStep step, ExecutionResult result, WorkflowExecutorResult wfResult)
    {
        if (result.Proceed)
        {
            pointer.Status = PointerStatus.Complete;
            pointer.EndTime = DateTime.UtcNow;
            pointer.Outcome = result.OutcomeValue;

            if (result.SleepFor.HasValue)
            {
                pointer.SleepUntil = DateTime.UtcNow.Add(result.SleepFor.Value);
                pointer.Status = PointerStatus.Sleeping;
            }
            else if (!string.IsNullOrEmpty(result.EventName))
            {
                pointer.Status = PointerStatus.WaitingForEvent;
                pointer.EventName = result.EventName;
                pointer.EventKey = result.EventKey;
            }
            else
            {
                // Find next steps
                var outcomes = step.Outcomes.Where(x => x.GetValue(workflow.Data) == result.OutcomeValue || result.OutcomeValue == null).ToList();
                if (outcomes.Count == 0)
                {
                    outcomes = step.Outcomes.Where(x => x.GetValue(workflow.Data) == null).ToList();
                }

                foreach (var outcome in outcomes)
                {
                    var nextPointer = new ExecutionPointer
                    {
                        Id = Guid.NewGuid().ToString(),
                        StepId = outcome.NextStep,
                        Status = PointerStatus.Pending,
                        Active = true,
                        PredecessorId = pointer.Id
                    };
                    workflow.ExecutionPointers.Add(nextPointer);
                }
            }
        }
        else
        {
            pointer.PersistenceData = result.PersistenceData;
            if (result.SleepFor.HasValue)
            {
                pointer.SleepUntil = DateTime.UtcNow.Add(result.SleepFor.Value);
                pointer.Status = PointerStatus.Sleeping;
            }
            else if (!string.IsNullOrEmpty(result.EventName))
            {
                pointer.Status = PointerStatus.WaitingForEvent;
                pointer.EventName = result.EventName;
                pointer.EventKey = result.EventKey;
            }
        }

        if (workflow.ExecutionPointers.FindActive().All(x => x.Status == PointerStatus.Complete || x.Status == PointerStatus.Sleeping || x.Status == PointerStatus.WaitingForEvent))
        {
            workflow.Status = WorkflowStatus.Complete;
            workflow.CompleteTime = DateTime.UtcNow;
        }
    }

    private void ProcessAfterExecutionIteration(WorkflowInstance workflow, WorkflowDefinition workflowDef, WorkflowExecutorResult workflowResult)
    {
        var pointers = workflow.ExecutionPointers.Where(x => x.EndTime == null);

        foreach (var pointer in pointers)
        {
            var step = workflowDef.Steps.FindById(pointer.StepId);
            step?.AfterWorkflowIteration(workflowResult, workflowDef, workflow, pointer);
        }
    }

    private void DetermineNextExecutionTime(WorkflowInstance workflow, WorkflowDefinition def)
    {
        workflow.NextExecution = null;

        if (workflow.Status == WorkflowStatus.Complete)
        {
            return;
        }

        foreach (var pointer in workflow.ExecutionPointers.Where(x => x.Active && x.Children.Count == 0))
        {
            if (!pointer.SleepUntil.HasValue)
            {
                workflow.NextExecution = 0;
                return;
            }

            var pointerSleep = pointer.SleepUntil.Value.ToUniversalTime().Ticks;
            workflow.NextExecution = Math.Min(pointerSleep, workflow.NextExecution ?? pointerSleep);
        }
    }
}
