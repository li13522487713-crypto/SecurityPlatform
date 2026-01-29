using Atlas.WorkflowCore.Abstractions;
using Atlas.WorkflowCore.Models;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Text.Json;

namespace Atlas.WorkflowCore.Services;

/// <summary>
/// 执行结果处理器实现
/// </summary>
public class ExecutionResultProcessor : IExecutionResultProcessor
{
    private readonly IExecutionPointerFactory _pointerFactory;
    private readonly ILifeCycleEventPublisher _eventPublisher;
    private readonly IEnumerable<IWorkflowErrorHandler> _errorHandlers;
    private readonly IWorkflowController _workflowController;
    private readonly ILogger<ExecutionResultProcessor> _logger;

    public ExecutionResultProcessor(
        IExecutionPointerFactory pointerFactory,
        ILifeCycleEventPublisher eventPublisher,
        IEnumerable<IWorkflowErrorHandler> errorHandlers,
        IWorkflowController workflowController,
        ILogger<ExecutionResultProcessor> logger)
    {
        _pointerFactory = pointerFactory;
        _eventPublisher = eventPublisher;
        _errorHandlers = errorHandlers;
        _workflowController = workflowController;
        _logger = logger;
    }

    public async Task ProcessExecutionResult(
        WorkflowInstance workflow,
        WorkflowDefinition definition,
        ExecutionPointer pointer,
        WorkflowStep step,
        ExecutionResult result,
        WorkflowExecutorResult workflowResult,
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

            // 创建事件订阅并添加到结果中
            var subscription = new EventSubscription
            {
                Id = Guid.NewGuid().ToString(),
                WorkflowId = workflow.Id,
                StepId = step.Id,
                ExecutionPointerId = pointer.Id,
                EventName = result.EventName!,
                EventKey = result.EventKey!,
                SubscribeAsOf = DateTime.UtcNow,
                SubscriptionData = result.SubscriptionData != null 
                    ? JsonSerializer.Serialize(result.SubscriptionData) 
                    : null
            };
            workflowResult.Subscriptions.Add(subscription);

            _logger.LogDebug("步骤 {StepName} 等待事件: {EventName} - {EventKey}",
                step.Name, result.EventName, result.EventKey);

            return;
        }

        // 3. 保存持久化数据
        if (result.PersistenceData != null)
        {
            pointer.PersistenceData = result.PersistenceData;
            
            // 【子工作流特殊处理】检测是否需要启动子工作流
            if (await ShouldStartChildWorkflowAsync(pointer, step, cancellationToken))
            {
                var childInstanceId = await StartChildWorkflowAsync(
                    workflow, pointer, step, cancellationToken);
                
                // 更新 PersistenceData 包含子工作流实例ID
                if (pointer.PersistenceData is Dictionary<string, object> dict)
                {
                    dict["ChildWorkflowInstanceId"] = childInstanceId;
                }
                
                // 重新激活指针，让步骤再次执行（进入等待事件状态）
                pointer.Status = PointerStatus.Pending;
                pointer.Active = true;
                
                _logger.LogInformation("子工作流 {ChildInstanceId} 已启动，父工作流 {ParentId} 将等待其完成",
                    childInstanceId, workflow.Id);
                return;
            }
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
                    var branchPointer = _pointerFactory.BuildChildPointer(branchStep, pointer, branchValue.ToString() ?? "");
                    workflow.ExecutionPointers.Add(branchPointer);
                    _logger.LogDebug("创建分支执行指针: {StepName}", branchStep.Name);
                }
            }

            pointer.Status = PointerStatus.Complete;
            pointer.Active = false;

            // 发布步骤完成事件
            await PublishStepCompletedEvent(workflow, definition, pointer, step, cancellationToken);
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
                var nextPointer = _pointerFactory.BuildNextPointer(nextStep, pointer);
                workflow.ExecutionPointers.Add(nextPointer);
                _logger.LogDebug("创建下一步执行指针: {StepName}", nextStep.Name);
            }
            else
            {
                _logger.LogDebug("步骤 {StepName} 没有后续步骤", step.Name);
            }

            // 发布步骤完成事件
            await PublishStepCompletedEvent(workflow, definition, pointer, step, cancellationToken);

            // 处理 PendingPredecessor 状态 - 激活等待此步骤的后续步骤
            await ProcessPendingPredecessors(workflow, definition, pointer, cancellationToken);
        }
        else
        {
            // 不继续执行
            pointer.Status = PointerStatus.Complete;
            pointer.Active = false;
        }
    }

    /// <summary>
    /// 发布步骤完成事件
    /// </summary>
    private async Task PublishStepCompletedEvent(
        WorkflowInstance workflow,
        WorkflowDefinition definition,
        ExecutionPointer pointer,
        WorkflowStep step,
        CancellationToken cancellationToken)
    {
        var evt = new Models.LifeCycleEvents.StepCompleted
        {
            WorkflowInstanceId = workflow.Id,
            WorkflowDefinitionId = definition.Id,
            Version = definition.Version,
            StepId = step.Id,
            StepName = step.Name,
            ExecutionPointerId = pointer.Id
        };

        await _eventPublisher.PublishNotificationAsync(evt, cancellationToken);
        _logger.LogDebug("步骤完成事件已发布: {StepName}", step.Name);
    }

    /// <summary>
    /// 处理等待前置步骤的指针（PendingPredecessor）
    /// </summary>
    private Task ProcessPendingPredecessors(
        WorkflowInstance workflow,
        WorkflowDefinition definition,
        ExecutionPointer completedPointer,
        CancellationToken cancellationToken)
    {
        // 查找所有等待当前指针完成的后续指针
        var pendingPointers = workflow.ExecutionPointers
            .Where(p => p.Status == PointerStatus.PendingPredecessor && p.PredecessorId == completedPointer.Id)
            .ToList();

        foreach (var pendingPointer in pendingPointers)
        {
            // 激活等待的指针
            pendingPointer.Status = PointerStatus.Pending;
            pendingPointer.Active = true;
            _logger.LogDebug("激活等待前置步骤的指针: {PointerId}", pendingPointer.Id);
        }

        return Task.CompletedTask;
    }

    public async Task HandleStepException(
        WorkflowInstance workflow,
        WorkflowDefinition definition,
        ExecutionPointer pointer,
        WorkflowStep step,
        Exception exception)
    {
        _logger.LogError(exception, "步骤 {StepName} 执行失败", step.Name);

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

        // 调用错误处理器链
        var errorBehavior = step.ErrorBehavior ?? WorkflowErrorHandling.Retry;
        var handler = _errorHandlers.FirstOrDefault(h => h.Type == errorBehavior);

        if (handler != null)
        {
            try
            {
                await handler.HandleAsync(workflow, definition, pointer, step, exception, CancellationToken.None);
                _logger.LogInformation("错误处理器 {HandlerType} 已处理异常", errorBehavior);
            }
            catch (Exception handlerEx)
            {
                _logger.LogError(handlerEx, "错误处理器执行失败");
                // 如果错误处理器失败，设置指针为失败状态
                pointer.Status = PointerStatus.Failed;
                pointer.Active = false;
            }
        }
        else
        {
            // 没有找到匹配的错误处理器，设置为失败
            _logger.LogWarning("未找到错误处理器: {ErrorBehavior}", errorBehavior);
            pointer.Status = PointerStatus.Failed;
            pointer.Active = false;
        }

        // 判断是否需要触发补偿
        if (ShouldCompensate(workflow, definition, pointer))
        {
            _logger.LogInformation("步骤执行失败，触发补偿逻辑");
            await TriggerCompensation(workflow, definition, pointer);
        }
    }

    /// <summary>
    /// 判断是否需要补偿
    /// </summary>
    private bool ShouldCompensate(WorkflowInstance workflow, WorkflowDefinition definition, ExecutionPointer pointer)
    {
        // 遍历作用域栈，检查是否有步骤配置了补偿处理
        var scope = new Stack<string>(pointer.Scope.Reverse());
        scope.Push(pointer.Id);

        while (scope.Count > 0)
        {
            var pointerId = scope.Pop();
            var scopePointer = workflow.ExecutionPointers.FindById(pointerId);
            if (scopePointer == null)
                continue;

            var scopeStep = definition.Steps.FindById(scopePointer.StepId);
            if (scopeStep == null)
                continue;

            // 检查是否有补偿步骤配置或需要回滚同级步骤
            if (scopeStep.CompensationStepId.HasValue || scopeStep.RevertChildrenAfterCompensation)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 触发补偿逻辑
    /// 注意：此方法已被 CompensateHandler 替代，保留用于向后兼容
    /// </summary>
    private Task TriggerCompensation(
        WorkflowInstance workflow,
        WorkflowDefinition definition,
        ExecutionPointer failedPointer)
    {
        // 补偿逻辑现在由 CompensateHandler 处理
        // 此方法保留用于向后兼容，但实际补偿逻辑在错误处理器中执行
        _logger.LogDebug("触发补偿逻辑（由 CompensateHandler 处理）");
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

    /// <summary>
    /// 检测是否需要启动子工作流
    /// </summary>
    /// <remarks>
    /// 判断逻辑：
    /// 1. 步骤类型必须是 SubWorkflowStepBody
    /// 2. PersistenceData 必须包含子工作流信息（ChildWorkflowId）
    /// 3. PersistenceData 不能已经包含子工作流实例ID（ChildWorkflowInstanceId）
    /// </remarks>
    private Task<bool> ShouldStartChildWorkflowAsync(
        ExecutionPointer pointer,
        WorkflowStep step,
        CancellationToken cancellationToken)
    {
        // 检查步骤类型是否是子工作流
        if (step.BodyType != typeof(Primitives.SubWorkflowStepBody))
        {
            return Task.FromResult(false);
        }

        // 检查 PersistenceData 格式和内容
        if (pointer.PersistenceData is Dictionary<string, object> dict)
        {
            var hasChildWorkflowId = dict.ContainsKey("ChildWorkflowId");
            var hasChildInstanceId = dict.ContainsKey("ChildWorkflowInstanceId");
            
            // 有子工作流ID但没有实例ID，说明需要启动
            return Task.FromResult(hasChildWorkflowId && !hasChildInstanceId);
        }

        return Task.FromResult(false);
    }

    /// <summary>
    /// 启动子工作流
    /// </summary>
    /// <returns>子工作流实例ID</returns>
    private async Task<string> StartChildWorkflowAsync(
        WorkflowInstance parentWorkflow,
        ExecutionPointer pointer,
        WorkflowStep step,
        CancellationToken cancellationToken)
    {
        if (pointer.PersistenceData is not Dictionary<string, object> dict)
        {
            throw new InvalidOperationException("PersistenceData 格式不正确");
        }

        // 提取子工作流参数
        var childWorkflowId = dict["ChildWorkflowId"]?.ToString()
            ?? throw new InvalidOperationException("ChildWorkflowId 不能为空");

        var childVersion = dict.TryGetValue("ChildWorkflowVersion", out var ver)
            ? Convert.ToInt32(ver)
            : (int?)null;

        // 解析子工作流数据
        object? childData = null;
        if (dict.TryGetValue("ChildWorkflowData", out var data) && data != null)
        {
            var dataStr = data.ToString();
            if (!string.IsNullOrEmpty(dataStr) && dataStr != "null")
            {
                try
                {
                    childData = JsonSerializer.Deserialize<object>(dataStr);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "无法反序列化子工作流数据，将使用原始数据");
                    childData = data;
                }
            }
        }

        // 构建子工作流引用（标识父子关系）
        var reference = $"{parentWorkflow.Id}:{pointer.Id}";

        _logger.LogInformation(
            "父工作流 {ParentId} 步骤 {StepName} 启动子工作流 {ChildId} (版本: {Version})",
            parentWorkflow.Id, step.Name, childWorkflowId, childVersion ?? -1);

        // 启动子工作流
        var childInstanceId = await _workflowController.StartWorkflowAsync(
            childWorkflowId,
            childVersion,
            childData,
            reference,
            cancellationToken);

        _logger.LogInformation(
            "子工作流实例 {ChildInstanceId} 已创建（父工作流: {ParentId}, 父步骤: {StepName}）",
            childInstanceId, parentWorkflow.Id, step.Name);

        return childInstanceId;
    }
}
