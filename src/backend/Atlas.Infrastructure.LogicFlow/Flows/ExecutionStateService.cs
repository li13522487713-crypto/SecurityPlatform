using Atlas.Application.LogicFlow.Flows.Abstractions;
using Atlas.Application.LogicFlow.Flows.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Tenancy;
using Atlas.Domain.LogicFlow.Flows;

namespace Atlas.Infrastructure.LogicFlow.Flows;

public sealed class ExecutionStateService : IExecutionStateService
{
    private readonly IFlowExecutionRepository _flowExecutions;
    private readonly INodeRunRepository _nodeRuns;
    private readonly ILogicFlowRepository _logicFlows;
    private readonly IIdGeneratorAccessor _idGen;

    public ExecutionStateService(
        IFlowExecutionRepository flowExecutions,
        INodeRunRepository nodeRuns,
        ILogicFlowRepository logicFlows,
        IIdGeneratorAccessor idGen)
    {
        _flowExecutions = flowExecutions;
        _nodeRuns = nodeRuns;
        _logicFlows = logicFlows;
        _idGen = idGen;
    }

    public async Task<long> StartExecutionAsync(
        long flowDefId,
        string? inputJson,
        TenantId tenantId,
        string userId,
        CancellationToken cancellationToken)
    {
        var flow = await _logicFlows.GetByIdAsync(flowDefId, cancellationToken)
            ?? throw new BusinessException("NOT_FOUND", "逻辑流不存在");
        if (flow.TenantIdValue != tenantId.Value)
            throw new BusinessException("NOT_FOUND", "逻辑流不存在");

        var id = _idGen.NextId();
        var execution = new FlowExecution(
            tenantId,
            flowDefId,
            flow.Version,
            flow.TriggerType,
            userId,
            inputJson,
            null,
            flow.MaxRetries,
            flow.SnapshotId,
            null)
        {
            Id = id,
        };
        execution.Start();
        await _flowExecutions.AddAsync(execution, cancellationToken);
        return id;
    }

    public async Task CompleteExecutionAsync(long executionId, string? outputJson, CancellationToken cancellationToken)
    {
        var execution = await _flowExecutions.GetByIdAsync(executionId, cancellationToken)
            ?? throw new BusinessException("NOT_FOUND", "执行实例不存在");
        execution.Complete(outputJson);
        await _flowExecutions.UpdateAsync(execution, cancellationToken);
    }

    public async Task FailExecutionAsync(long executionId, string errorMessage, CancellationToken cancellationToken)
    {
        var execution = await _flowExecutions.GetByIdAsync(executionId, cancellationToken)
            ?? throw new BusinessException("NOT_FOUND", "执行实例不存在");
        execution.Fail(errorMessage);
        await _flowExecutions.UpdateAsync(execution, cancellationToken);
    }

    public async Task CancelExecutionAsync(long executionId, CancellationToken cancellationToken)
    {
        var execution = await _flowExecutions.GetByIdAsync(executionId, cancellationToken)
            ?? throw new BusinessException("NOT_FOUND", "执行实例不存在");
        execution.Cancel();
        await _flowExecutions.UpdateAsync(execution, cancellationToken);
    }

    public async Task<long> StartNodeRunAsync(
        long executionId,
        string nodeKey,
        string nodeTypeKey,
        string? inputJson,
        CancellationToken cancellationToken)
    {
        var execution = await _flowExecutions.GetByIdAsync(executionId, cancellationToken)
            ?? throw new BusinessException("NOT_FOUND", "执行实例不存在");
        var id = _idGen.NextId();
        var run = new NodeRun(execution.TenantId, executionId, nodeKey, nodeTypeKey, inputJson, execution.MaxRetries)
        {
            Id = id,
        };
        run.Start();
        await _nodeRuns.AddAsync(run, cancellationToken);
        return id;
    }

    public async Task CompleteNodeRunAsync(long nodeRunId, string? outputJson, CancellationToken cancellationToken)
    {
        var run = await _nodeRuns.GetByIdAsync(nodeRunId, cancellationToken)
            ?? throw new BusinessException("NOT_FOUND", "节点运行记录不存在");
        run.Complete(outputJson);
        await _nodeRuns.UpdateAsync(run, cancellationToken);
    }

    public async Task FailNodeRunAsync(long nodeRunId, string errorMessage, bool canRetry, CancellationToken cancellationToken)
    {
        var run = await _nodeRuns.GetByIdAsync(nodeRunId, cancellationToken)
            ?? throw new BusinessException("NOT_FOUND", "节点运行记录不存在");
        run.Fail(errorMessage, canRetry);
        await _nodeRuns.UpdateAsync(run, cancellationToken);
    }
}
