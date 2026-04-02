using System.Text.Json;
using Atlas.Application.LogicFlow.Flows.Abstractions;
using Atlas.Application.LogicFlow.Flows.Models;
using Atlas.Application.LogicFlow.Flows.Repositories;
using Atlas.Core.Exceptions;
using Atlas.Core.Tenancy;
using Atlas.Domain.LogicFlow.Flows;
using Atlas.Infrastructure.LogicFlow.Flows;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.LogicFlow.Services;

public sealed class FlowExecutionCommandService : IFlowExecutionCommandService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly ILogicFlowRepository _logicFlows;
    private readonly IFlowExecutionRepository _executions;
    private readonly INodeRunRepository _nodeRuns;
    private readonly IExecutionStateService _state;
    private readonly IFlowCompiler _compiler;
    private readonly IDagScheduler _scheduler;
    private readonly INodeExecutor _nodeExecutor;
    private readonly ILogger<FlowExecutionCommandService> _logger;

    public FlowExecutionCommandService(
        ILogicFlowRepository logicFlows,
        IFlowExecutionRepository executions,
        INodeRunRepository nodeRuns,
        IExecutionStateService state,
        IFlowCompiler compiler,
        IDagScheduler scheduler,
        INodeExecutor nodeExecutor,
        ILogger<FlowExecutionCommandService> logger)
    {
        _logicFlows = logicFlows;
        _executions = executions;
        _nodeRuns = nodeRuns;
        _state = state;
        _compiler = compiler;
        _scheduler = scheduler;
        _nodeExecutor = nodeExecutor;
        _logger = logger;
    }

    public async Task<long> TriggerAsync(
        FlowExecutionTriggerRequest request,
        TenantId tenantId,
        string userId,
        CancellationToken cancellationToken)
    {
        var flow = await _logicFlows.GetByIdAsync(request.FlowDefinitionId, cancellationToken)
            ?? throw new BusinessException("NOT_FOUND", "逻辑流不存在");
        if (flow.TenantIdValue != tenantId.Value)
            throw new BusinessException("NOT_FOUND", "逻辑流不存在");
        if (flow.Status != FlowStatus.Published || !flow.IsEnabled)
            throw new BusinessException("LOGIC_FLOW_NOT_PUBLISHED", "逻辑流未发布或未启用，无法触发执行");
        if (!flow.SnapshotId.HasValue || flow.SnapshotId.Value <= 0)
            throw new BusinessException("LOGIC_FLOW_NOT_PUBLISHED", "逻辑流缺少已发布快照绑定，无法触发执行");

        var executionId = await _state.StartExecutionAsync(
            request.FlowDefinitionId,
            request.InputDataJson,
            tenantId,
            userId,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.CorrelationId))
        {
            var patch = await _executions.GetByIdAsync(executionId, cancellationToken);
            if (patch is not null)
            {
                patch.CorrelationId = request.CorrelationId;
                await _executions.UpdateAsync(patch, cancellationToken);
            }
        }

        var plan = await _compiler.CompileAsync(request.FlowDefinitionId, tenantId, cancellationToken);
        var context = new ExecutionContextImpl(executionId, request.FlowDefinitionId, tenantId, request.CorrelationId);
        SeedContextFromInput(context, request.InputDataJson);

        await RunDagAsync(executionId, flow, plan, context, cancellationToken);
        return executionId;
    }

    public async Task CancelAsync(long executionId, TenantId tenantId, CancellationToken cancellationToken)
    {
        var execution = await RequireExecutionAsync(executionId, tenantId, cancellationToken);
        if (execution.Status is ExecutionStatus.Completed
            or ExecutionStatus.Failed
            or ExecutionStatus.Cancelled
            or ExecutionStatus.TimedOut
            or ExecutionStatus.Compensated)
            throw new BusinessException("INVALID_STATE", "当前状态不可取消");
        await _state.CancelExecutionAsync(executionId, cancellationToken);
    }

    public async Task<long> RetryAsync(long executionId, TenantId tenantId, string userId, CancellationToken cancellationToken)
    {
        var prior = await RequireExecutionAsync(executionId, tenantId, cancellationToken);
        if (prior.Status is not (ExecutionStatus.Failed or ExecutionStatus.TimedOut or ExecutionStatus.Cancelled))
            throw new BusinessException("INVALID_STATE", "仅失败、超时或已取消的执行可重试");

        var flow = await _logicFlows.GetByIdAsync(prior.FlowDefinitionId, cancellationToken)
            ?? throw new BusinessException("NOT_FOUND", "逻辑流不存在");
        if (flow.Status != FlowStatus.Published || !flow.IsEnabled)
            throw new BusinessException("LOGIC_FLOW_NOT_PUBLISHED", "逻辑流未发布或未启用，无法重试执行");
        if (!flow.SnapshotId.HasValue || flow.SnapshotId.Value <= 0)
            throw new BusinessException("LOGIC_FLOW_NOT_PUBLISHED", "逻辑流缺少已发布快照绑定，无法重试执行");

        var newId = await _state.StartExecutionAsync(
            prior.FlowDefinitionId,
            prior.InputDataJson,
            tenantId,
            userId,
            cancellationToken);

        var neo = await _executions.GetByIdAsync(newId, cancellationToken);
        if (neo is not null)
        {
            neo.ParentExecutionId = executionId;
            neo.CorrelationId = prior.CorrelationId;
            await _executions.UpdateAsync(neo, cancellationToken);
        }

        var plan = await _compiler.CompileAsync(prior.FlowDefinitionId, tenantId, cancellationToken);
        var context = new ExecutionContextImpl(newId, prior.FlowDefinitionId, tenantId, prior.CorrelationId);
        SeedContextFromInput(context, prior.InputDataJson);
        await RunDagAsync(newId, flow, plan, context, cancellationToken);
        return newId;
    }

    public async Task PauseAsync(long executionId, TenantId tenantId, CancellationToken cancellationToken)
    {
        var execution = await RequireExecutionAsync(executionId, tenantId, cancellationToken);
        if (execution.Status != ExecutionStatus.Running)
            throw new BusinessException("INVALID_STATE", "仅运行中的执行可暂停");
        execution.Pause();
        await _executions.UpdateAsync(execution, cancellationToken);
    }

    public async Task ResumeAsync(long executionId, TenantId tenantId, CancellationToken cancellationToken)
    {
        var execution = await RequireExecutionAsync(executionId, tenantId, cancellationToken);
        if (execution.Status != ExecutionStatus.Paused)
            throw new BusinessException("INVALID_STATE", "仅已暂停的执行可恢复");
        execution.Resume();
        await _executions.UpdateAsync(execution, cancellationToken);
    }

    private async Task<FlowExecution> RequireExecutionAsync(
        long executionId,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var execution = await _executions.GetByIdAsync(executionId, cancellationToken)
            ?? throw new BusinessException("NOT_FOUND", "执行实例不存在");
        if (execution.TenantIdValue != tenantId.Value)
            throw new BusinessException("NOT_FOUND", "执行实例不存在");
        return execution;
    }

    private static void SeedContextFromInput(ExecutionContextImpl context, string? inputJson)
    {
        if (string.IsNullOrWhiteSpace(inputJson))
            return;
        try
        {
            var doc = JsonDocument.Parse(inputJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in doc.RootElement.EnumerateObject())
                    context.SetVariable(prop.Name, prop.Value.ToString());
            }
            else
                context.SetVariable("input", inputJson);
        }
        catch
        {
            context.SetVariable("input", inputJson);
        }
    }

    private async Task RunDagAsync(
        long executionId,
        LogicFlowDefinition flow,
        PhysicalDagPlan plan,
        ExecutionContextImpl context,
        CancellationToken cancellationToken)
    {
        var completed = new HashSet<string>(StringComparer.Ordinal);
        var timeoutSeconds = flow.TimeoutSeconds > 0 ? flow.TimeoutSeconds : 300;
        using var dagCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        dagCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            while (completed.Count < plan.Nodes.Count)
            {
                if (!await IsExecutionContinuableAsync(executionId, dagCts.Token))
                    return;

                var ready = _scheduler.GetReadySet(plan, completed);
                if (ready.Count == 0)
                {
                    await _state.FailExecutionAsync(executionId, "调度无法继续：存在未就绪节点或依赖异常", dagCts.Token);
                    return;
                }

                var readyNodes = plan.Nodes
                    .Where(n => ready.Contains(n.NodeKey))
                    .ToList();

                var batchResults = await Task.WhenAll(
                    readyNodes.Select(async dagNode =>
                    {
                        if (!await IsExecutionContinuableAsync(executionId, dagCts.Token))
                            return (dagNode.NodeKey, Ok: false);
                        var ok = await ExecuteNodeWithRetriesAsync(
                            executionId,
                            flow,
                            plan,
                            dagNode,
                            context,
                            dagCts.Token);
                        return (dagNode.NodeKey, Ok: ok);
                    }));

                if (batchResults.Any(x => !x.Ok))
                    return;

                foreach (var result in batchResults)
                    completed.Add(result.NodeKey);
            }

            if (!await IsExecutionContinuableAsync(executionId, dagCts.Token))
                return;

            var outputJson = JsonSerializer.Serialize(context.Variables, JsonOptions);
            await _state.CompleteExecutionAsync(executionId, outputJson, dagCts.Token);
        }
        catch (OperationCanceledException) when (dagCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            var execution = await _executions.GetByIdAsync(executionId, cancellationToken);
            if (execution is not null)
            {
                execution.Timeout();
                await _executions.UpdateAsync(execution, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "逻辑流 DAG 执行异常: execution={ExecutionId}", executionId);
            await _state.FailExecutionAsync(executionId, ex.Message, cancellationToken);
        }
    }

    private async Task<bool> IsExecutionContinuableAsync(long executionId, CancellationToken cancellationToken)
    {
        var execution = await _executions.GetByIdAsync(executionId, cancellationToken);
        return execution is { Status: ExecutionStatus.Running };
    }

    private async Task<bool> ExecuteNodeWithRetriesAsync(
        long executionId,
        LogicFlowDefinition flow,
        PhysicalDagPlan plan,
        PhysicalDagNode dagNode,
        ExecutionContextImpl context,
        CancellationToken cancellationToken)
    {
        var execution = await _executions.GetByIdAsync(executionId, cancellationToken)
            ?? throw new BusinessException("NOT_FOUND", "执行实例不存在");

        var nodeRunId = await _state.StartNodeRunAsync(
            executionId,
            dagNode.NodeKey,
            dagNode.TypeKey,
            execution.InputDataJson,
            cancellationToken);

        execution.CurrentNodeKey = dagNode.NodeKey;
        await _executions.UpdateAsync(execution, cancellationToken);

        var dispatcherAttempt = 0;
        while (true)
        {
            var nodeEntity = await _nodeRuns.GetByIdAsync(nodeRunId, cancellationToken)
                ?? throw new BusinessException("NOT_FOUND", "节点运行记录不存在");

            var request = new NodeExecutionRequest
            {
                FlowExecutionId = executionId,
                NodeKey = dagNode.NodeKey,
                TypeKey = dagNode.TypeKey,
                ConfigJson = dagNode.ConfigJson,
                InputData = BuildNodeInputDictionary(execution.InputDataJson, nodeEntity.InputDataJson),
                RetryAttempt = dispatcherAttempt,
                MaxRetries = nodeEntity.MaxRetries,
                TimeoutSeconds = flow.TimeoutSeconds,
            };

            var result = await _nodeExecutor.ExecuteAsync(request, cancellationToken);
            if (result.IsSuccess)
            {
                var outputJson = JsonSerializer.Serialize(result.OutputData, JsonOptions);
                await _state.CompleteNodeRunAsync(nodeRunId, outputJson, cancellationToken);
                context.SetNodeOutput(dagNode.NodeKey, result.OutputData);
                return true;
            }

            var canRetry = nodeEntity.RetryCount < nodeEntity.MaxRetries;
            await _state.FailNodeRunAsync(nodeRunId, result.ErrorMessage ?? "NODE_FAILED", canRetry, cancellationToken);

            var updated = await _nodeRuns.GetByIdAsync(nodeRunId, cancellationToken);
            if (updated is null || updated.Status != NodeRunStatus.WaitingForRetry)
            {
                var branchRecovered = await TryExecuteFailureBranchAsync(
                    executionId,
                    flow,
                    plan,
                    dagNode,
                    context,
                    result.ErrorMessage ?? "NODE_FAILED",
                    cancellationToken);
                if (branchRecovered)
                {
                    return true;
                }

                await _state.FailExecutionAsync(executionId, result.ErrorMessage ?? "NODE_FAILED", cancellationToken);
                return false;
            }

            updated.Retry();
            updated.Start();
            await _nodeRuns.UpdateAsync(updated, cancellationToken);
            dispatcherAttempt++;
        }
    }

    private static Dictionary<string, string> BuildNodeInputDictionary(string flowInputJson, string nodeInputJson)
    {
        var dict = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["flowInput"] = string.IsNullOrWhiteSpace(flowInputJson) ? "{}" : flowInputJson,
            ["nodeInput"] = string.IsNullOrWhiteSpace(nodeInputJson) ? "{}" : nodeInputJson,
        };
        return dict;
    }

    private async Task<bool> TryExecuteFailureBranchAsync(
        long executionId,
        LogicFlowDefinition flow,
        PhysicalDagPlan plan,
        PhysicalDagNode failedNode,
        ExecutionContextImpl context,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        var errorEdge = plan.Edges.FirstOrDefault(e =>
            string.Equals(e.SourceNodeKey, failedNode.NodeKey, StringComparison.Ordinal) &&
            string.Equals(e.SourcePortKey, "error", StringComparison.OrdinalIgnoreCase));

        if (errorEdge is not null)
        {
            var targetNode = plan.Nodes.FirstOrDefault(n =>
                string.Equals(n.NodeKey, errorEdge.TargetNodeKey, StringComparison.Ordinal));
            if (targetNode is not null)
            {
                _logger.LogWarning(
                    "节点执行失败，转入错误分支: execution={ExecutionId}, node={NodeKey}, target={TargetNode}, reason={Reason}",
                    executionId,
                    failedNode.NodeKey,
                    targetNode.NodeKey,
                    errorMessage);
                return await ExecuteBranchNodeAsync(executionId, flow, targetNode, context, cancellationToken);
            }
        }

        var compensationEdge = plan.Edges.FirstOrDefault(e =>
            string.Equals(e.SourceNodeKey, failedNode.NodeKey, StringComparison.Ordinal) &&
            string.Equals(e.SourcePortKey, "compensation", StringComparison.OrdinalIgnoreCase));

        if (compensationEdge is not null)
        {
            var targetNode = plan.Nodes.FirstOrDefault(n =>
                string.Equals(n.NodeKey, compensationEdge.TargetNodeKey, StringComparison.Ordinal));
            if (targetNode is not null)
            {
                _logger.LogWarning(
                    "节点执行失败，触发补偿分支: execution={ExecutionId}, node={NodeKey}, target={TargetNode}, reason={Reason}",
                    executionId,
                    failedNode.NodeKey,
                    targetNode.NodeKey,
                    errorMessage);
                return await ExecuteBranchNodeAsync(executionId, flow, targetNode, context, cancellationToken);
            }
        }

        return false;
    }

    private async Task<bool> ExecuteBranchNodeAsync(
        long executionId,
        LogicFlowDefinition flow,
        PhysicalDagNode branchNode,
        ExecutionContextImpl context,
        CancellationToken cancellationToken)
    {
        var request = new NodeExecutionRequest
        {
            FlowExecutionId = executionId,
            NodeKey = branchNode.NodeKey,
            TypeKey = branchNode.TypeKey,
            ConfigJson = branchNode.ConfigJson,
            InputData = new Dictionary<string, string>(StringComparer.Ordinal),
            RetryAttempt = 0,
            MaxRetries = 0,
            TimeoutSeconds = flow.TimeoutSeconds,
        };

        var branchResult = await _nodeExecutor.ExecuteAsync(request, cancellationToken);
        if (!branchResult.IsSuccess)
        {
            return false;
        }

        context.SetNodeOutput(branchNode.NodeKey, branchResult.OutputData);
        return true;
    }
}
