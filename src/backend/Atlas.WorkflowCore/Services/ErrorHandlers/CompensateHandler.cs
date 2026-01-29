using Atlas.WorkflowCore.Abstractions;
using Atlas.WorkflowCore.Models;
using Microsoft.Extensions.Logging;

namespace Atlas.WorkflowCore.Services.ErrorHandlers;

/// <summary>
/// 补偿错误处理器
/// </summary>
public class CompensateHandler : IWorkflowErrorHandler
{
    private readonly IExecutionPointerFactory _pointerFactory;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly WorkflowOptions _options;
    private readonly ILogger<CompensateHandler> _logger;

    public CompensateHandler(
        IExecutionPointerFactory pointerFactory,
        IDateTimeProvider dateTimeProvider,
        WorkflowOptions options,
        ILogger<CompensateHandler> logger)
    {
        _pointerFactory = pointerFactory;
        _dateTimeProvider = dateTimeProvider;
        _options = options;
        _logger = logger;
    }

    public WorkflowErrorHandling Type => WorkflowErrorHandling.Compensate;

    public Task HandleAsync(
        WorkflowInstance workflow,
        WorkflowDefinition definition,
        ExecutionPointer exceptionPointer,
        WorkflowStep exceptionStep,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var scope = new Stack<string>(exceptionPointer.Scope.Reverse());
        scope.Push(exceptionPointer.Id);
        ExecutionPointer? compensationPointer = null;

        while (scope.Count > 0)
        {
            var pointerId = scope.Pop();
            var scopePointer = workflow.ExecutionPointers.FindById(pointerId);
            if (scopePointer == null)
            {
                _logger.LogWarning("执行指针 {PointerId} 未找到", pointerId);
                continue;
            }

            var scopeStep = definition.Steps.FindById(scopePointer.StepId);
            if (scopeStep == null)
            {
                _logger.LogWarning("步骤 {StepId} 未找到", scopePointer.StepId);
                continue;
            }

            // 检查父级步骤的 ResumeChildrenAfterCompensation 和 RevertChildrenAfterCompensation
            var resume = true;
            var revert = false;

            var txnStack = new Stack<string>(scope.Reverse());
            while (txnStack.Count > 0)
            {
                var parentId = txnStack.Pop();
                var parentPointer = workflow.ExecutionPointers.FindById(parentId);
                if (parentPointer == null)
                    continue;

                var parentStep = definition.Steps.FindById(parentPointer.StepId);
                if (parentStep == null)
                    continue;

                if ((!parentStep.ResumeChildrenAfterCompensation) || (parentStep.RevertChildrenAfterCompensation))
                {
                    resume = parentStep.ResumeChildrenAfterCompensation;
                    revert = parentStep.RevertChildrenAfterCompensation;
                    break;
                }
            }

            // 如果步骤的错误处理策略不是 Compensate，则向上冒泡
            var errorBehavior = scopeStep.ErrorBehavior ?? definition.DefaultErrorBehavior;
            if (errorBehavior != WorkflowErrorHandling.Compensate)
            {
                _logger.LogDebug("步骤 {StepName} 的错误处理策略不是 Compensate，跳过", scopeStep.Name);
                continue;
            }

            // 标记作用域指针为失败
            scopePointer.Active = false;
            scopePointer.EndTime = _dateTimeProvider.UtcNow;
            scopePointer.Status = PointerStatus.Failed;

            // 如果步骤有补偿步骤配置，创建补偿指针
            if (scopeStep.CompensationStepId.HasValue)
            {
                scopePointer.Status = PointerStatus.Compensated;

                var nextCompensationPointer = _pointerFactory.BuildCompensationPointer(
                    definition,
                    scopePointer,
                    exceptionPointer,
                    scopeStep.CompensationStepId.Value);

                // 链式连接补偿指针
                if (compensationPointer != null)
                {
                    nextCompensationPointer.Active = false;
                    nextCompensationPointer.Status = PointerStatus.PendingPredecessor;
                    nextCompensationPointer.PredecessorId = compensationPointer.Id;
                }

                compensationPointer = nextCompensationPointer;
                workflow.ExecutionPointers.Add(compensationPointer);

                _logger.LogInformation("创建补偿指针: {StepName} (工作流: {WorkflowId})",
                    scopeStep.Name, workflow.Id);

                // 如果 ResumeChildrenAfterCompensation = true，继续执行后续步骤
                if (resume)
                {
                    foreach (var outcome in scopeStep.Outcomes)
                    {
                        if (MatchesOutcome(outcome, workflow.Data))
                        {
                            var nextPointer = _pointerFactory.BuildNextPointer(
                                definition.Steps.FindById(outcome.NextStep) ?? throw new InvalidOperationException($"Next step {outcome.NextStep} not found"),
                                scopePointer);
                            workflow.ExecutionPointers.Add(nextPointer);
                            _logger.LogDebug("补偿后继续执行后续步骤: {NextStepId}", outcome.NextStep);
                        }
                    }
                }
            }

            // 如果 RevertChildrenAfterCompensation = true，回滚同级已完成步骤
            if (revert)
            {
                var prevSiblings = workflow.ExecutionPointers
                    .Where(x => scopePointer.Scope.SequenceEqual(x.Scope) &&
                               x.Id != scopePointer.Id &&
                               x.Status == PointerStatus.Complete)
                    .OrderByDescending(x => x.EndTime)
                    .ToList();

                foreach (var siblingPointer in prevSiblings)
                {
                    var siblingStep = definition.Steps.FindById(siblingPointer.StepId);
                    if (siblingStep == null)
                        continue;

                    if (siblingStep.CompensationStepId.HasValue)
                    {
                        var nextCompensationPointer = _pointerFactory.BuildCompensationPointer(
                            definition,
                            siblingPointer,
                            exceptionPointer,
                            siblingStep.CompensationStepId.Value);

                        // 链式连接补偿指针
                        if (compensationPointer != null)
                        {
                            nextCompensationPointer.Active = false;
                            nextCompensationPointer.Status = PointerStatus.PendingPredecessor;
                            nextCompensationPointer.PredecessorId = compensationPointer.Id;
                        }

                        compensationPointer = nextCompensationPointer;
                        workflow.ExecutionPointers.Add(nextCompensationPointer);

                        siblingPointer.Status = PointerStatus.Compensated;

                        _logger.LogInformation("回滚同级步骤并创建补偿指针: {StepName} (工作流: {WorkflowId})",
                            siblingStep.Name, workflow.Id);
                    }
                }
            }
        }

        _logger.LogError(exception,
            "补偿逻辑处理完成 (工作流: {WorkflowId}, 异常步骤: {StepName})",
            workflow.Id, exceptionStep.Name);

        return Task.CompletedTask;
    }

    /// <summary>
    /// 匹配结果值
    /// </summary>
    private bool MatchesOutcome(IStepOutcome outcome, object? data)
    {
        if (outcome is ValueOutcome valueOutcome)
        {
            return valueOutcome.Matches(data, valueOutcome.Value);
        }

        // 对于表达式结果，使用反射调用 Matches 方法
        var outcomeType = outcome.GetType();
        if (outcomeType.IsGenericType && outcomeType.GetGenericTypeDefinition().Name.StartsWith("ExpressionOutcome"))
        {
            var matchesMethod = outcomeType.GetMethod("Matches", new[] { typeof(object), typeof(object) });
            if (matchesMethod != null)
            {
                try
                {
                    var result = matchesMethod.Invoke(outcome, new[] { data ?? new object(), null });
                    return result is bool boolResult && boolResult;
                }
                catch
                {
                    // 如果调用失败，默认匹配
                    return true;
                }
            }
        }

        // 对于其他类型，默认匹配
        return true;
    }
}
