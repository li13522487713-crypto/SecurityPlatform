using Atlas.WorkflowCore.Abstractions;
using Atlas.WorkflowCore.Models;
using Microsoft.Extensions.Logging;

namespace Atlas.WorkflowCore.Services;

/// <summary>
/// 执行结果处理器实现
/// </summary>
public class ExecutionResultProcessor : IExecutionResultProcessor
{
    private readonly IExecutionPointerFactory _pointerFactory;
    private readonly ILogger<ExecutionResultProcessor> _logger;

    public ExecutionResultProcessor(
        IExecutionPointerFactory pointerFactory,
        ILogger<ExecutionResultProcessor> logger)
    {
        _pointerFactory = pointerFactory;
        _logger = logger;
    }

    public async Task ProcessExecutionResult(
        WorkflowInstance workflow,
        WorkflowDefinition definition,
        ExecutionPointer pointer,
        WorkflowStep step,
        ExecutionResult result,
        CancellationToken cancellationToken)
    {
        pointer.EndTime = DateTime.UtcNow;

        // 1. 处理休眠
        if (result.SleepFor.HasValue)
        {
            pointer.Status = PointerStatus.Sleeping;
            pointer.SleepUntil = DateTime.UtcNow.Add(result.SleepFor.Value);
            pointer.Active = false;
            workflow.NextExecution = DateTimeOffset.UtcNow.Add(result.SleepFor.Value).ToUnixTimeMilliseconds();
            _logger.LogDebug("步骤 {StepName} 将休眠至 {SleepUntil}", step.Name, pointer.SleepUntil);
            return;
        }

        // 2. 处理事件订阅
        if (!string.IsNullOrEmpty(result.EventName))
        {
            pointer.Status = PointerStatus.WaitingForEvent;
            pointer.EventName = result.EventName;
            pointer.EventKey = result.EventKey;
            pointer.Active = false;
            pointer.EventPublished = false;

            _logger.LogDebug("步骤 {StepName} 等待事件: {EventName} - {EventKey}",
                step.Name, result.EventName, result.EventKey);

            // 事件订阅将在 WorkflowHost 中创建
            return;
        }

        // 3. 保存持久化数据
        if (result.PersistenceData != null)
        {
            pointer.PersistenceData = result.PersistenceData;
        }

        // 4. 处理分支值
        if (result.BranchValues != null && result.BranchValues.Count > 0)
        {
            foreach (var branchValue in result.BranchValues)
            {
                // 查找匹配的分支步骤
                var branchStep = FindBranchStep(definition, step, branchValue);

                if (branchStep != null)
                {
                    var branchPointer = _pointerFactory.CreateChildPointer(branchStep, pointer, branchValue.ToString() ?? "");
                    workflow.ExecutionPointers.Add(branchPointer);
                    _logger.LogDebug("创建分支执行指针: {StepName}", branchStep.Name);
                }
            }

            pointer.Status = PointerStatus.Complete;
            pointer.Active = false;
            return;
        }

        // 5. 处理步骤完成
        if (result.Proceed)
        {
            pointer.Status = PointerStatus.Complete;
            pointer.Active = false;

            // 查找下一步
            var nextStep = FindNextStep(definition, step, result.OutcomeValue);

            if (nextStep != null)
            {
                var nextPointer = _pointerFactory.CreateNextPointer(nextStep, pointer);
                workflow.ExecutionPointers.Add(nextPointer);
                _logger.LogDebug("创建下一步执行指针: {StepName}", nextStep.Name);
            }
            else
            {
                _logger.LogDebug("步骤 {StepName} 没有后续步骤", step.Name);
            }
        }
        else
        {
            // 不继续执行
            pointer.Status = PointerStatus.Complete;
            pointer.Active = false;
        }

        await Task.CompletedTask;
    }

    public Task HandleStepException(
        WorkflowInstance workflow,
        WorkflowDefinition definition,
        ExecutionPointer pointer,
        WorkflowStep step,
        Exception exception)
    {
        _logger.LogError(exception, "步骤 {StepName} 执行失败", step.Name);

        pointer.Status = PointerStatus.Failed;
        pointer.Active = false;
        pointer.EndTime = DateTime.UtcNow;

        // 记录错误
        workflow.ExecutionErrors.Add(new ExecutionError
        {
            WorkflowId = workflow.Id,
            ExecutionPointerId = pointer.Id,
            ErrorTime = DateTime.UtcNow,
            Message = exception.Message,
            StackTrace = exception.StackTrace
        });

        // 错误处理策略将在后续版本实现
        return Task.CompletedTask;
    }

    private WorkflowStep? FindNextStep(WorkflowDefinition definition, WorkflowStep currentStep, object? outcomeValue)
    {
        // 查找匹配的结果
        var matchingOutcome = currentStep.Outcomes.FirstOrDefault(o => MatchesOutcome(o, outcomeValue));

        if (matchingOutcome == null)
        {
            return null;
        }

        // 查找下一步
        return definition.Steps.FindById(matchingOutcome.NextStep);
    }

    private WorkflowStep? FindBranchStep(WorkflowDefinition definition, WorkflowStep currentStep, object branchValue)
    {
        // 简化实现：查找子步骤
        if (currentStep.Children != null && currentStep.Children.Count > 0)
        {
            var childStepId = currentStep.Children.First();
            return definition.Steps.FindById(childStepId);
        }
        return null;
    }

    private bool MatchesOutcome(IStepOutcome outcome, object? value)
    {
        if (outcome is ValueOutcome valueOutcome)
        {
            if (valueOutcome.Value == null && value == null)
            {
                return true;
            }

            if (valueOutcome.Value != null && value != null)
            {
                return valueOutcome.Value.Equals(value);
            }
        }

        return false;
    }
}
