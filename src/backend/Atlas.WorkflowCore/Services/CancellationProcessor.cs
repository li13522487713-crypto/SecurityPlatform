using Atlas.WorkflowCore.Abstractions;
using Atlas.WorkflowCore.Models;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace Atlas.WorkflowCore.Services;

/// <summary>
/// 取消处理器实现
/// </summary>
public class CancellationProcessor : ICancellationProcessor
{
    private readonly IExecutionResultProcessor _executionResultProcessor;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<CancellationProcessor> _logger;

    public CancellationProcessor(
        IExecutionResultProcessor executionResultProcessor,
        IDateTimeProvider dateTimeProvider,
        ILogger<CancellationProcessor> logger)
    {
        _executionResultProcessor = executionResultProcessor;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public void ProcessCancellations(WorkflowInstance workflow, WorkflowDefinition workflowDef, WorkflowExecutorResult executionResult)
    {
        // 查找所有有取消条件的步骤
        foreach (var step in workflowDef.Steps)
        {
            // 检查步骤是否有取消条件（通过反射或属性）
            var cancelConditionProperty = step.GetType().GetProperty("CancelCondition");
            if (cancelConditionProperty == null)
                continue;

            var cancelCondition = cancelConditionProperty.GetValue(step);
            if (cancelCondition == null)
                continue;

            // 编译并执行取消条件表达式
            bool cancel = false;
            try
            {
                if (cancelCondition is LambdaExpression lambda)
                {
                    var func = lambda.Compile();
                    cancel = (bool)(func.DynamicInvoke(workflow.Data) ?? false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取消条件执行失败: {StepName}", step.Name);
            }

            if (cancel)
            {
                // 查找需要取消的执行指针
                var toCancel = workflow.ExecutionPointers
                    .Where(x => x.StepId == step.Id &&
                               x.Status != PointerStatus.Complete &&
                               x.Status != PointerStatus.Cancelled)
                    .ToList();

                foreach (var ptr in toCancel)
                {
                    // 如果 ProceedOnCancel = true，继续执行后续步骤
                    if (step.ProceedOnCancel)
                    {
                        _executionResultProcessor.ProcessExecutionResult(
                            workflow,
                            workflowDef,
                            ptr,
                            step,
                            ExecutionResult.Next(),
                            executionResult);
                    }

                    // 标记指针为已取消
                    ptr.EndTime = _dateTimeProvider.UtcNow;
                    ptr.Active = false;
                    ptr.Status = PointerStatus.Cancelled;

                    // 取消所有子指针
                    var descendants = workflow.ExecutionPointers
                        .FindByScope(ptr.Id)
                        .Where(x => x.Status != PointerStatus.Complete &&
                                   x.Status != PointerStatus.Cancelled)
                        .ToList();

                    foreach (var descendant in descendants)
                    {
                        descendant.EndTime = _dateTimeProvider.UtcNow;
                        descendant.Active = false;
                        descendant.Status = PointerStatus.Cancelled;
                    }

                    _logger.LogInformation("步骤 {StepName} 已被取消 (工作流: {WorkflowId})", step.Name, workflow.Id);
                }
            }
        }
    }
}
