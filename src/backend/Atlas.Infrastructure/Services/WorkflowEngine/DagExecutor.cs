using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Atlas.Application.AiPlatform.Models;
using Atlas.Application.AiPlatform.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.AiPlatform.Enums;
using Atlas.Domain.AiPlatform.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services.WorkflowEngine;

/// <summary>
/// V2 DAG 执行引擎——按拓扑顺序执行工作流画布中的节点。
/// </summary>
public sealed class DagExecutor
{
    private const int MaxGraphTraversalDepth = 2048;
    private const int MaxLoopIterations = 1000;
    private const int CompiledCanvasCacheMaxSize = 256;

    // RT-07: 按画布 JSON 哈希缓存拓扑编译结果，避免每次执行重复计算。
    private static readonly ConcurrentDictionary<string, CompiledCanvas> _canvasCache = new();
    private static int _canvasCacheSize;
    private readonly NodeExecutorRegistry _registry;
    private readonly IWorkflowNodeExecutionRepository _nodeExecutionRepo;
    private readonly IWorkflowExecutionRepository _executionRepo;
    private readonly IIdGeneratorAccessor _idGenerator;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DagExecutor> _logger;

    public DagExecutor(
        NodeExecutorRegistry registry,
        IWorkflowNodeExecutionRepository nodeExecutionRepo,
        IWorkflowExecutionRepository executionRepo,
        IIdGeneratorAccessor idGenerator,
        IServiceProvider serviceProvider,
        ILogger<DagExecutor> logger)
    {
        _registry = registry;
        _nodeExecutionRepo = nodeExecutionRepo;
        _executionRepo = executionRepo;
        _idGenerator = idGenerator;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _logger.LogDebug("DagExecutor initialized.");
    }

    /// <summary>
    /// 同步执行工作流 DAG 图。
    /// </summary>
    public async Task RunAsync(
        TenantId tenantId,
        WorkflowExecution execution,
        CanvasSchema canvas,
        Dictionary<string, JsonElement> inputs,
        Channel<SseEvent>? eventChannel,
        CancellationToken cancellationToken,
        IReadOnlyList<long>? workflowCallStack = null,
        IReadOnlySet<string>? preCompletedNodeKeys = null,
        long? userId = null,
        string? channelId = null)
    {
        _logger.LogInformation(
            "DagExecutor run start: ExecutionId={ExecutionId} NodeCount={NodeCount} ConnectionCount={ConnectionCount}",
            execution.Id,
            canvas.Nodes.Count,
            canvas.Connections.Count);
        execution.Start();
        await _executionRepo.UpdateAsync(execution, cancellationToken);
        // RT-20: 执行指标收集。
        var executionSw = Stopwatch.StartNew();
        var executedNodeCount = 0;
        var databaseEnvironment = execution.IsDebug || execution.VersionNumber == 0
            ? AiDatabaseRecordEnvironment.Draft
            : AiDatabaseRecordEnvironment.Online;

        var variables = new Dictionary<string, JsonElement>(inputs, StringComparer.OrdinalIgnoreCase);
        var currentCallStack = workflowCallStack is not null && workflowCallStack.Count > 0
            ? workflowCallStack
            : new[] { execution.WorkflowId };

        try
        {
            // RT-07: 从缓存加载或编译画布拓扑结构。
            var (nodeMap, topology) = GetOrCompileCanvas(canvas);
            var adjacency = topology.Adjacency;
            var predecessors = topology.Predecessors;
            var connectionsBySource = topology.ConnectionsBySource;
            var executionLevels = topology.ExecutionLevels;
            var preCompletedNodeKeySet = preCompletedNodeKeys is null
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(preCompletedNodeKeys, StringComparer.OrdinalIgnoreCase);
            var skippedNodeKeys = preCompletedNodeKeys is null
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(preCompletedNodeKeys, StringComparer.OrdinalIgnoreCase);
            var persistedSkippedNodeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var level in executionLevels)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var newlySkippedByPredecessor = ResolveNodesWithAllSkippedPredecessors(level, predecessors, skippedNodeKeys);
                foreach (var nodeKey in newlySkippedByPredecessor)
                {
                    skippedNodeKeys.Add(nodeKey);
                }
                await PersistSkippedNodesAsync(
                    tenantId,
                    execution.Id,
                    level.Where(nodeKey =>
                        skippedNodeKeys.Contains(nodeKey) &&
                        !preCompletedNodeKeySet.Contains(nodeKey)),
                    nodeMap,
                    connectionsBySource,
                    persistedSkippedNodeKeys,
                    eventChannel,
                    cancellationToken);

                var executableNodeKeys = level
                    .Where(nodeKey => !skippedNodeKeys.Contains(nodeKey))
                    .ToArray();
                if (executableNodeKeys.Length == 0)
                {
                    continue;
                }

                var levelInput = new Dictionary<string, JsonElement>(variables, StringComparer.OrdinalIgnoreCase);
                var levelTasks = executableNodeKeys
                    .Select(nodeKey => ExecuteNodeAsync(
                        tenantId,
                        execution.WorkflowId,
                        execution.Id,
                        currentCallStack,
                        nodeKey,
                        nodeMap,
                        connectionsBySource,
                        levelInput,
                        eventChannel,
                        cancellationToken,
                        userId,
                        channelId,
                        execution.IsDebug,
                        databaseEnvironment))
                    .ToArray();

                // RT-12: 将每个并行任务包装为安全捕获模式，防止单个 OperationCanceledException 导致整层丢失结果。
                var levelResults = await Task.WhenAll(levelTasks.Select(t => t.ContinueWith(
                    tt => tt.IsFaulted
                        ? NodeRunResult.FailedResult(
                            "unknown",
                            WorkflowNodeType.TextProcessor,
                            $"节点执行抛出未预期异常: {tt.Exception?.GetBaseException().Message ?? "未知错误"}",
                            InterruptType.None)
                        : tt.IsCanceled
                            ? NodeRunResult.FailedResult("unknown", WorkflowNodeType.TextProcessor, "执行已取消", InterruptType.None)
                            : tt.Result,
                    TaskContinuationOptions.ExecuteSynchronously)));

                var failedNode = levelResults.FirstOrDefault(x => !x.Success);
                if (failedNode is not null)
                {
                    if (failedNode.InterruptType != InterruptType.None)
                    {
                        execution.Interrupt(failedNode.InterruptType, failedNode.NodeKey);
                        await _executionRepo.UpdateAsync(execution, cancellationToken);
                        return;
                    }

                    // RT-05: 检查失败节点是否配置了 on_error 策略或存在 error 分支连接。
                    var onErrorHandled = await TryHandleNodeErrorAsync(
                        tenantId,
                        execution,
                        failedNode,
                        nodeMap,
                        connectionsBySource,
                        variables,
                        skippedNodeKeys,
                        eventChannel,
                        cancellationToken);

                    if (onErrorHandled)
                    {
                        continue;
                    }

                    await PersistBlockedByFailureAsync(
                        tenantId,
                        execution.Id,
                        failedNode.NodeKey,
                        failedNode.ErrorMessage ?? "上游节点失败",
                        nodeMap,
                        adjacency,
                        connectionsBySource,
                        skippedNodeKeys,
                        preCompletedNodeKeySet,
                        eventChannel,
                        cancellationToken);
                    execution.Fail(failedNode.ErrorMessage ?? "节点执行失败");
                    await _executionRepo.UpdateAsync(execution, cancellationToken);
                    return;
                }

                // 同层全部成功后再合并输出，避免失败层产生部分提交。
                foreach (var result in levelResults)
                {
                    MergeNodeOutputs(variables, result);
                    executedNodeCount++;
                }

                // 条件分支节点执行完成后，按选中分支跳过未命中的下游节点。
                foreach (var selectorNode in levelResults.Where(x => x.Success && x.NodeType == WorkflowNodeType.Selector))
                {
                    var newlySkippedNodeKeys = new List<string>();
                    foreach (var nodeKeyToSkip in ResolveSelectorBranchNodesToSkip(
                                 selectorNode.NodeKey,
                                 selectorNode.Outputs,
                                 adjacency,
                                 connectionsBySource))
                    {
                        if (skippedNodeKeys.Add(nodeKeyToSkip))
                        {
                            newlySkippedNodeKeys.Add(nodeKeyToSkip);
                        }
                    }

                    await PersistSkippedNodesAsync(
                        tenantId,
                        execution.Id,
                        newlySkippedNodeKeys.Where(nodeKey => !preCompletedNodeKeySet.Contains(nodeKey)),
                        nodeMap,
                        connectionsBySource,
                        persistedSkippedNodeKeys,
                        eventChannel,
                        cancellationToken);
                }

                var loopNodes = levelResults
                    .Where(x =>
                        x.Success &&
                        x.NodeType == WorkflowNodeType.Loop &&
                        !IsLoopCompleted(x.Outputs))
                    .ToArray();

                foreach (var loopNode in loopNodes)
                {
                    var loopResult = await ExecuteLoopIterationsAsync(
                        tenantId,
                        execution.WorkflowId,
                        execution.Id,
                        currentCallStack,
                        loopNode.NodeKey,
                        nodeMap,
                        adjacency,
                        connectionsBySource,
                        variables,
                        eventChannel,
                        cancellationToken,
                        userId,
                        channelId,
                        execution.IsDebug,
                        databaseEnvironment);

                    if (!loopResult.Success)
                    {
                        if (loopResult.InterruptType != InterruptType.None)
                        {
                            execution.Interrupt(loopResult.InterruptType, loopResult.NodeKey);
                            await _executionRepo.UpdateAsync(execution, cancellationToken);
                            return;
                        }

                        await PersistBlockedByFailureAsync(
                            tenantId,
                            execution.Id,
                            loopResult.NodeKey,
                            loopResult.ErrorMessage ?? "循环节点执行失败",
                            nodeMap,
                            adjacency,
                            connectionsBySource,
                            skippedNodeKeys,
                            preCompletedNodeKeySet,
                            eventChannel,
                            cancellationToken);
                        execution.Fail(loopResult.ErrorMessage ?? "循环节点执行失败");
                        await _executionRepo.UpdateAsync(execution, cancellationToken);
                        return;
                    }

                    foreach (var bodyNodeKey in loopResult.BodyNodeKeysToSkip)
                    {
                        skippedNodeKeys.Add(bodyNodeKey);
                    }

                    await PersistSkippedNodesAsync(
                        tenantId,
                        execution.Id,
                        loopResult.BodyNodeKeysToSkip.Where(nodeKey => !preCompletedNodeKeySet.Contains(nodeKey)),
                        nodeMap,
                        connectionsBySource,
                        persistedSkippedNodeKeys,
                        eventChannel,
                        cancellationToken);
                }

                var batchNodes = levelResults
                    .Where(x => x.Success && x.NodeType == WorkflowNodeType.Batch)
                    .ToArray();
                foreach (var batchNode in batchNodes)
                {
                    var batchResult = await ExecuteBatchSubCanvasAsync(
                        tenantId,
                        execution.WorkflowId,
                        execution.Id,
                        currentCallStack,
                        batchNode.NodeKey,
                        nodeMap,
                        variables,
                        eventChannel,
                        cancellationToken,
                        userId,
                        channelId,
                        execution.IsDebug,
                        databaseEnvironment);

                    if (!batchResult.Success)
                    {
                        if (batchResult.InterruptType != InterruptType.None)
                        {
                            execution.Interrupt(batchResult.InterruptType, batchResult.NodeKey);
                            await _executionRepo.UpdateAsync(execution, cancellationToken);
                            return;
                        }

                        await PersistBlockedByFailureAsync(
                            tenantId,
                            execution.Id,
                            batchResult.NodeKey,
                            batchResult.ErrorMessage ?? "批处理节点执行失败",
                            nodeMap,
                            adjacency,
                            connectionsBySource,
                            skippedNodeKeys,
                            preCompletedNodeKeySet,
                            eventChannel,
                            cancellationToken);
                        execution.Fail(batchResult.ErrorMessage ?? "批处理节点执行失败");
                        await _executionRepo.UpdateAsync(execution, cancellationToken);
                        return;
                    }

                    MergeNodeOutputs(variables, batchResult);
                }
            }

            // 全部执行完毕
            cancellationToken.ThrowIfCancellationRequested();
            var latestExecution = await _executionRepo.FindByIdAsync(tenantId, execution.Id, CancellationToken.None);
            if (latestExecution?.Status == ExecutionStatus.Cancelled)
            {
                _logger.LogInformation("执行已被取消，跳过完成态回写: ExecutionId={ExecutionId}", execution.Id);
                return;
            }

            executionSw.Stop();

            // RT-20: 写入执行指标。
            _logger.LogInformation(
                "DagExecutor run complete: ExecutionId={ExecutionId} TotalMs={TotalMs} ExecutedNodes={ExecutedNodes}",
                execution.Id,
                executionSw.ElapsedMilliseconds,
                executedNodeCount);

            execution.Complete(JsonSerializer.Serialize(variables));
            await _executionRepo.UpdateAsync(execution, cancellationToken);

            if (eventChannel is not null)
            {
                await eventChannel.Writer.WriteAsync(
                    new SseEvent("execution_metrics", JsonSerializer.Serialize(new
                    {
                        executionId = execution.Id.ToString(),
                        totalMs = executionSw.ElapsedMilliseconds,
                        executedNodeCount
                    })),
                    CancellationToken.None);
            }
        }
        catch (OperationCanceledException)
        {
            execution.Cancel();
            await _executionRepo.UpdateAsync(execution, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "工作流执行异常: ExecutionId={ExecutionId}", execution.Id);
            execution.Fail(ex.Message);
            await _executionRepo.UpdateAsync(execution, CancellationToken.None);
        }
        finally
        {
            eventChannel?.Writer.TryComplete();
        }
    }

    /// <summary>
    /// 从 CanvasJson 反序列化 CanvasSchema。
    /// </summary>
    public static CanvasSchema? ParseCanvas(string canvasJson)
    {
        return WorkflowCanvasJsonBridge.TryParseCanvas(canvasJson, out var canvas)
            ? canvas
            : null;
    }

    private static (IReadOnlyDictionary<string, NodeSchema> NodeMap, CompiledCanvas Topology) GetOrCompileCanvas(CanvasSchema canvas)
    {
        // NodeMap 始终从原始画布重新构建，保证 Config 数据的正确性。
        var nodeMap = canvas.Nodes.ToDictionary(n => n.Key, n => n, StringComparer.OrdinalIgnoreCase);

        var cacheKey = ComputeCanvasHash(canvas);
        if (_canvasCache.TryGetValue(cacheKey, out var cached))
        {
            return (nodeMap, cached);
        }

        var adjacency = BuildAdjacency(canvas);
        var predecessors = BuildPredecessors(canvas);
        var connectionsBySource = BuildConnectionsBySource(canvas);
        var executionLevels = TopologicalSortByLevels(canvas.Nodes, adjacency);
        var compiled = new CompiledCanvas(adjacency, predecessors, connectionsBySource, executionLevels);

        // 限制缓存大小，避免无限增长。
        if (Interlocked.Increment(ref _canvasCacheSize) <= CompiledCanvasCacheMaxSize)
        {
            _canvasCache.TryAdd(cacheKey, compiled);
        }
        else
        {
            Interlocked.Decrement(ref _canvasCacheSize);
        }

        return (nodeMap, compiled);
    }

    private static string ComputeCanvasHash(CanvasSchema canvas)
    {
        var json = JsonSerializer.Serialize(new
        {
            nodes = canvas.Nodes.Select(n => new { n.Key, n.Type }).OrderBy(n => n.Key),
            connections = canvas.Connections.Select(c => new { c.SourceNodeKey, c.SourcePort, c.TargetNodeKey, c.TargetPort })
                .OrderBy(c => c.SourceNodeKey).ThenBy(c => c.TargetNodeKey)
        });
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hashBytes)[..16];
    }

    private static Dictionary<string, List<string>> BuildAdjacency(CanvasSchema canvas)
    {
        var adjacency = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in canvas.Nodes)
        {
            adjacency.TryAdd(node.Key, new List<string>());
        }

        foreach (var conn in canvas.Connections)
        {
            if (adjacency.TryGetValue(conn.SourceNodeKey, out var targets))
            {
                targets.Add(conn.TargetNodeKey);
            }
        }

        return adjacency;
    }

    private static Dictionary<string, List<string>> BuildPredecessors(CanvasSchema canvas)
    {
        var predecessors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in canvas.Nodes)
        {
            predecessors.TryAdd(node.Key, new List<string>());
        }

        foreach (var connection in canvas.Connections)
        {
            if (!predecessors.TryGetValue(connection.TargetNodeKey, out var sources))
            {
                sources = new List<string>();
                predecessors[connection.TargetNodeKey] = sources;
            }

            sources.Add(connection.SourceNodeKey);
        }

        return predecessors;
    }

    private static Dictionary<string, List<ConnectionSchema>> BuildConnectionsBySource(CanvasSchema canvas)
    {
        var map = new Dictionary<string, List<ConnectionSchema>>(StringComparer.OrdinalIgnoreCase);
        foreach (var connection in canvas.Connections)
        {
            if (!map.TryGetValue(connection.SourceNodeKey, out var list))
            {
                list = new List<ConnectionSchema>();
                map[connection.SourceNodeKey] = list;
            }

            list.Add(connection);
        }

        return map;
    }

    private async Task<NodeRunResult> ExecuteNodeAsync(
        TenantId tenantId,
        long workflowId,
        long executionId,
        IReadOnlyList<long> workflowCallStack,
        string nodeKey,
        IReadOnlyDictionary<string, NodeSchema> nodeMap,
        IReadOnlyDictionary<string, List<ConnectionSchema>> connectionsBySource,
        Dictionary<string, JsonElement> inputVariables,
        Channel<SseEvent>? eventChannel,
        CancellationToken cancellationToken,
        long? userId = null,
        string? channelId = null,
        bool isDebug = false,
        AiDatabaseRecordEnvironment databaseEnvironment = AiDatabaseRecordEnvironment.Draft)
    {
        if (!nodeMap.TryGetValue(nodeKey, out var node))
        {
            return NodeRunResult.SuccessResult(nodeKey, null, EmptyOutputs);
        }

        _logger.LogWarning(
            "DagExecutor node start: ExecutionId={ExecutionId} NodeKey={NodeKey} NodeType={NodeType}",
            executionId,
            nodeKey,
            node.Type);
        _logger.LogDebug(
            "DagExecutor node start: ExecutionId={ExecutionId} NodeKey={NodeKey} NodeType={NodeType}",
            executionId,
            nodeKey,
            node.Type);

        var executor = _registry.GetExecutor(node.Type);
        if (executor is null)
        {
            // Comment 节点是纯画布注释，无运行时语义，明确跳过。
            if (node.Type == WorkflowNodeType.Comment)
            {
                return NodeRunResult.SuccessResult(nodeKey, node.Type, EmptyOutputs);
            }

            // P0-3 修复（PLAN §P0-3 + 跨里程碑硬约束）：
            // 此前对未注册执行器的节点返回 SuccessResult 空输出，导致已声明的 NodeType 在画布上"静默吞业务"——
            // 比 NotImplementedException 更危险（用户看不到节点没跑）。
            // 现修正为返回 FailedResult + 明确错误码 NODE_EXECUTOR_NOT_REGISTERED，让 DagExecutor 走标准失败链路：
            //  - 通过 PersistBlockedByFailureAsync 把下游也标记 blocked
            //  - 错误信息暴露给前端 / trace / 日志
            // 这样上线时若忘了注册执行器，会立即在执行期暴露而非沉默。
            var msg = $"NODE_EXECUTOR_NOT_REGISTERED: 未注册节点类型 {node.Type} 的执行器（节点 {nodeKey}）。" +
                      $"请在 NodeExecutorRegistry._executorTypes 注册对应 INodeExecutor 实现。";
            _logger.LogError("{Message}", msg);
            return NodeRunResult.FailedResult(nodeKey, node.Type, msg, InterruptType.None);
        }

        var inputMaterialization = NodeInputMaterializer.Materialize(node, inputVariables);
        var preparedInputVariables = inputMaterialization.PreparedVariables;
        var nodeExec = new WorkflowNodeExecution(tenantId, executionId, nodeKey, node.Type, _idGenerator.NextId());
        nodeExec.Start(JsonSerializer.Serialize(inputMaterialization.Snapshot));
        await _nodeExecutionRepo.AddAsync(nodeExec, cancellationToken);

        if (eventChannel is not null)
        {
            await eventChannel.Writer.WriteAsync(
                new SseEvent("node_start", JsonSerializer.Serialize(new
                {
                    executionId = executionId.ToString(),
                    nodeKey,
                    nodeType = node.Type.ToString()
                })),
                cancellationToken);
            await EmitEdgeStatusChangedAsync(
                executionId,
                nodeKey,
                EdgeExecutionStatus.Incomplete,
                reason: "node_running",
                connectionsBySource,
                node.Type,
                outputs: null,
                eventChannel,
                cancellationToken);
        }

        var sw = Stopwatch.StartNew();
        var context = new NodeExecutionContext(
            node,
            preparedInputVariables,
            _serviceProvider,
            tenantId,
            workflowId,
            executionId,
            workflowCallStack,
            eventChannel,
            userId,
            channelId,
            isDebug,
            databaseEnvironment);
        var nodeTimeout = ResolveNodeTimeout(node);
        var resolvedNodeTimeout = nodeTimeout ?? TimeSpan.Zero;
        using var timeoutCts = nodeTimeout is null
            ? null
            : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (timeoutCts is not null)
        {
            timeoutCts.CancelAfter(resolvedNodeTimeout);
        }
        var executionToken = timeoutCts?.Token ?? cancellationToken;

        try
        {
            NodeExecutionResult result;
            try
            {
                result = await executor.ExecuteAsync(context, executionToken);
            }
            catch (OperationCanceledException) when (
                timeoutCts is not null &&
                timeoutCts.IsCancellationRequested &&
                !cancellationToken.IsCancellationRequested)
            {
                var timeoutMessage = $"节点执行超时（{resolvedNodeTimeout.TotalSeconds:0.#}s）";
                result = new NodeExecutionResult(
                    Success: false,
                    Outputs: new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase),
                    ErrorMessage: timeoutMessage);
            }

            sw.Stop();

            if (result.Success)
            {
                _logger.LogWarning(
                    "DagExecutor node success: ExecutionId={ExecutionId} NodeKey={NodeKey} OutputCount={OutputCount}",
                    executionId,
                    nodeKey,
                    result.Outputs.Count);
                _logger.LogDebug(
                    "DagExecutor node success: ExecutionId={ExecutionId} NodeKey={NodeKey} OutputCount={OutputCount}",
                    executionId,
                    nodeKey,
                    result.Outputs.Count);
                nodeExec.Complete(JsonSerializer.Serialize(result.Outputs), sw.ElapsedMilliseconds);
                await _nodeExecutionRepo.UpdateAsync(nodeExec, cancellationToken);

                if (eventChannel is not null)
                {
                    await EmitEdgeStatusChangedAsync(
                        executionId,
                        nodeKey,
                        EdgeExecutionStatus.Success,
                        reason: null,
                        connectionsBySource,
                        node.Type,
                        result.Outputs,
                        eventChannel,
                        cancellationToken);
                    await eventChannel.Writer.WriteAsync(
                        new SseEvent("node_output", JsonSerializer.Serialize(new
                        {
                            executionId = executionId.ToString(),
                            nodeKey,
                            nodeType = node.Type.ToString(),
                            outputs = result.Outputs
                        })),
                        cancellationToken);
                    await EmitBranchDecisionAsync(
                        executionId,
                        node,
                        result.Outputs,
                        eventChannel,
                        cancellationToken);

                    await eventChannel.Writer.WriteAsync(
                        new SseEvent("node_complete", JsonSerializer.Serialize(new
                        {
                            executionId = executionId.ToString(),
                            nodeKey,
                            nodeType = node.Type.ToString(),
                            durationMs = sw.ElapsedMilliseconds
                        })),
                        cancellationToken);
                }

                return NodeRunResult.SuccessResult(nodeKey, node.Type, result.Outputs);
            }

            _logger.LogWarning(
                "DagExecutor node failed: ExecutionId={ExecutionId} NodeKey={NodeKey} Error={Error}",
                executionId,
                nodeKey,
                result.ErrorMessage ?? "节点执行失败");
            nodeExec.Fail(result.ErrorMessage ?? "节点执行失败");
            await _nodeExecutionRepo.UpdateAsync(nodeExec, cancellationToken);
            if (eventChannel is not null)
            {
                await EmitEdgeStatusChangedAsync(
                    executionId,
                    nodeKey,
                    EdgeExecutionStatus.Failed,
                    result.ErrorMessage ?? "节点执行失败",
                    connectionsBySource,
                    node.Type,
                    null,
                    eventChannel,
                    cancellationToken);
                await eventChannel.Writer.WriteAsync(
                    new SseEvent("node_failed", JsonSerializer.Serialize(new
                    {
                        executionId = executionId.ToString(),
                        nodeKey,
                        nodeType = node.Type.ToString(),
                        durationMs = sw.ElapsedMilliseconds,
                        errorMessage = result.ErrorMessage ?? "节点执行失败",
                        interruptType = result.InterruptType.ToString()
                    })),
                    cancellationToken);
            }

            return NodeRunResult.FailedResult(nodeKey, node.Type, result.ErrorMessage, result.InterruptType);
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            _logger.LogWarning(
                "DagExecutor node canceled: ExecutionId={ExecutionId} NodeKey={NodeKey}",
                executionId,
                nodeKey);
            nodeExec.Fail("执行已取消");
            await _nodeExecutionRepo.UpdateAsync(nodeExec, cancellationToken);
            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "节点 {NodeKey} 执行异常", nodeKey);
            nodeExec.Fail(ex.Message);
            await _nodeExecutionRepo.UpdateAsync(nodeExec, cancellationToken);
            if (eventChannel is not null)
            {
                await EmitEdgeStatusChangedAsync(
                    executionId,
                    nodeKey,
                    EdgeExecutionStatus.Failed,
                    ex.Message,
                    connectionsBySource,
                    node.Type,
                    null,
                    eventChannel,
                    cancellationToken);
                await eventChannel.Writer.WriteAsync(
                    new SseEvent("node_failed", JsonSerializer.Serialize(new
                    {
                        executionId = executionId.ToString(),
                        nodeKey,
                        nodeType = node.Type.ToString(),
                        durationMs = sw.ElapsedMilliseconds,
                        errorMessage = ex.Message,
                        interruptType = InterruptType.None.ToString()
                    })),
                    cancellationToken);
            }

            return NodeRunResult.FailedResult(nodeKey, node.Type, $"节点 {nodeKey} 执行异常: {ex.Message}", InterruptType.None);
        }
    }

    private static async Task EmitBranchDecisionAsync(
        long executionId,
        NodeSchema node,
        IReadOnlyDictionary<string, JsonElement> outputs,
        Channel<SseEvent>? eventChannel,
        CancellationToken cancellationToken)
    {
        if (eventChannel is null)
        {
            return;
        }

        if (node.Type == WorkflowNodeType.Selector)
        {
            var selectedBranch = ResolveSelectedSelectorBranch(outputs);
            if (selectedBranch is null)
            {
                return;
            }

            await eventChannel.Writer.WriteAsync(
                new SseEvent("branch_decision", JsonSerializer.Serialize(new
                {
                    executionId = executionId.ToString(),
                    nodeKey = node.Key,
                    nodeType = node.Type.ToString(),
                    selectedBranch = selectedBranch == SelectorBranch.True ? "true_branch" : "false_branch",
                    candidates = new[] { "true_branch", "false_branch" }
                })),
                cancellationToken);
            return;
        }

        if (node.Type != WorkflowNodeType.IntentDetector)
        {
            return;
        }

        if (!outputs.TryGetValue("detected_intent", out var detectedIntentRaw))
        {
            return;
        }

        var selectedIntent = VariableResolver.ToDisplayText(detectedIntentRaw);
        if (string.IsNullOrWhiteSpace(selectedIntent))
        {
            return;
        }

        List<string>? candidates = null;
        if (node.Config.TryGetValue("intents", out var intentsRaw) && intentsRaw.ValueKind == JsonValueKind.Array)
        {
            candidates = intentsRaw
                .EnumerateArray()
                .Select(VariableResolver.ToDisplayText)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        await eventChannel.Writer.WriteAsync(
            new SseEvent("branch_decision", JsonSerializer.Serialize(new
            {
                executionId = executionId.ToString(),
                nodeKey = node.Key,
                nodeType = node.Type.ToString(),
                selectedBranch = selectedIntent,
                candidates
            })),
            cancellationToken);
    }

    private async Task<LoopIterationResult> ExecuteLoopIterationsAsync(
        TenantId tenantId,
        long workflowId,
        long executionId,
        IReadOnlyList<long> workflowCallStack,
        string loopNodeKey,
        IReadOnlyDictionary<string, NodeSchema> nodeMap,
        Dictionary<string, List<string>> adjacency,
        Dictionary<string, List<ConnectionSchema>> connectionsBySource,
        Dictionary<string, JsonElement> variables,
        Channel<SseEvent>? eventChannel,
        CancellationToken cancellationToken,
        long? userId = null,
        string? channelId = null,
        bool isDebug = false,
        AiDatabaseRecordEnvironment databaseEnvironment = AiDatabaseRecordEnvironment.Draft)
    {
        if (!nodeMap.TryGetValue(loopNodeKey, out var loopNode))
        {
            return LoopIterationResult.Succeeded(loopNodeKey, Array.Empty<string>());
        }

        var bodyNodeKeys = ResolveLoopBodyNodeKeys(loopNode, adjacency, connectionsBySource);
        if (bodyNodeKeys.Count == 0)
        {
            return LoopIterationResult.Succeeded(loopNodeKey, Array.Empty<string>());
        }

        var bodyLevels = TopologicalSortSubsetByLevels(bodyNodeKeys, adjacency);
        var iterationCount = 0;
        // 保存循环入口时的变量快照，每轮迭代从此基准叠加循环控制变量，实现作用域隔离。
        var loopEntrySnapshot = new Dictionary<string, JsonElement>(variables, StringComparer.OrdinalIgnoreCase);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            iterationCount++;
            if (iterationCount > MaxLoopIterations)
            {
                return LoopIterationResult.Failed(
                    loopNodeKey,
                    $"循环超过最大迭代次数限制（{MaxLoopIterations}），已强制终止，请检查循环条件。",
                    InterruptType.None);
            }

            // 每轮迭代从入口快照 + 当前循环控制变量重建作用域，隔离迭代间副作用。
            var iterationScope = new Dictionary<string, JsonElement>(loopEntrySnapshot, StringComparer.OrdinalIgnoreCase);
            var loopControlKeys = variables.Keys
                .Where(k => k.StartsWith($"{loopNodeKey}.locals.", StringComparison.OrdinalIgnoreCase) || 
                            new[] { "loop_index", "loop_item", "loop_item_index", "loop_completed", "loop_break_reason" }.Contains(k, StringComparer.OrdinalIgnoreCase))
                .ToList();

            foreach (var loopControlKey in loopControlKeys)
            {
                if (variables.TryGetValue(loopControlKey, out var ctrlVal))
                {
                    iterationScope[loopControlKey] = ctrlVal;
                }
            }

            foreach (var bodyLevel in bodyLevels)
            {
                var levelInput = new Dictionary<string, JsonElement>(iterationScope, StringComparer.OrdinalIgnoreCase);
                var bodyTasks = bodyLevel
                    .Select(nodeKey => ExecuteNodeAsync(
                        tenantId,
                        workflowId,
                        executionId,
                        workflowCallStack,
                        nodeKey,
                        nodeMap,
                        connectionsBySource,
                        levelInput,
                        eventChannel,
                        cancellationToken,
                        userId,
                        channelId,
                        isDebug,
                        databaseEnvironment))
                    .ToArray();

                var bodyResults = await Task.WhenAll(bodyTasks);
                var failedBodyNode = bodyResults.FirstOrDefault(x => !x.Success);
                if (failedBodyNode is not null)
                {
                    return LoopIterationResult.Failed(
                        failedBodyNode.NodeKey,
                        failedBodyNode.ErrorMessage,
                        failedBodyNode.InterruptType);
                }

                foreach (var bodyResult in bodyResults)
                {
                    MergeNodeOutputs(iterationScope, bodyResult);
                }

                if (HasControlSignal(iterationScope, "loop_break"))
                {
                    variables["loop_completed"] = JsonSerializer.SerializeToElement(true);
                    ClearControlSignal(iterationScope, "loop_break");
                    ClearControlSignal(iterationScope, "loop_continue");
                    return LoopIterationResult.Succeeded(loopNodeKey, bodyNodeKeys);
                }

                if (HasControlSignal(iterationScope, "loop_continue"))
                {
                    ClearControlSignal(iterationScope, "loop_continue");
                    goto NextLoopIteration;
                }
            }

            // 迭代执行完毕后，将本轮控制变量同步回父作用域。
            foreach (var loopControlKey in loopControlKeys)
            {
                if (iterationScope.TryGetValue(loopControlKey, out var ctrlVal))
                {
                    variables[loopControlKey] = ctrlVal;
                }
            }

            var loopInput = new Dictionary<string, JsonElement>(variables, StringComparer.OrdinalIgnoreCase);
            var loopResult = await ExecuteNodeAsync(
                tenantId,
                workflowId,
                executionId,
                workflowCallStack,
                loopNodeKey,
                nodeMap,
                connectionsBySource,
                loopInput,
                eventChannel,
                cancellationToken,
                userId,
                channelId,
                isDebug,
                databaseEnvironment);
            if (!loopResult.Success)
            {
                return LoopIterationResult.Failed(loopResult.NodeKey, loopResult.ErrorMessage, loopResult.InterruptType);
            }

            MergeNodeOutputs(variables, loopResult);

            if (IsLoopCompleted(loopResult.Outputs))
            {
                break;
            }

        NextLoopIteration:
            ;
        }

        return LoopIterationResult.Succeeded(loopNodeKey, bodyNodeKeys);
    }

    private static bool IsLoopCompleted(Dictionary<string, JsonElement> outputs)
    {
        if (!outputs.TryGetValue("loop_completed", out var completedRaw))
        {
            // 未提供 loop_completed 时不再继续迭代，避免异常配置导致死循环。
            return true;
        }

        if (VariableResolver.TryGetBoolean(completedRaw, out var completed))
        {
            return completed;
        }

        return string.Equals(VariableResolver.ToDisplayText(completedRaw), "1", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<NodeRunResult> ExecuteBatchSubCanvasAsync(
        TenantId tenantId,
        long workflowId,
        long executionId,
        IReadOnlyList<long> workflowCallStack,
        string batchNodeKey,
        IReadOnlyDictionary<string, NodeSchema> nodeMap,
        Dictionary<string, JsonElement> variables,
        Channel<SseEvent>? eventChannel,
        CancellationToken cancellationToken,
        long? userId = null,
        string? channelId = null,
        bool isDebug = false,
        AiDatabaseRecordEnvironment databaseEnvironment = AiDatabaseRecordEnvironment.Draft)
    {
        if (!nodeMap.TryGetValue(batchNodeKey, out var batchNode))
        {
            return NodeRunResult.SuccessResult(batchNodeKey, WorkflowNodeType.Batch, EmptyOutputs);
        }

        if (batchNode.ChildCanvas is null || batchNode.ChildCanvas.Nodes.Count == 0)
        {
            return NodeRunResult.SuccessResult(batchNodeKey, WorkflowNodeType.Batch, EmptyOutputs);
        }

        int concurrentSize = 4;
        int batchSize = 1;
        if (batchNode.Config.TryGetValue("inputs", out var inputsRaw) && inputsRaw.ValueKind == JsonValueKind.Object)
        {
            if (inputsRaw.TryGetProperty("concurrentSize", out var concExpr))
                concurrentSize = Math.Clamp(ExtractValueExpressionInt32(concExpr) ?? 4, 1, 64);
            if (inputsRaw.TryGetProperty("batchSize", out var batchExpr))
                batchSize = Math.Clamp(ExtractValueExpressionInt32(batchExpr) ?? 1, 1, 10_000);
        }

        string inputParamName = "input";
        if (inputsRaw.ValueKind == JsonValueKind.Object && 
            inputsRaw.TryGetProperty("inputParameters", out var inputParams) && 
            inputParams.ValueKind == JsonValueKind.Array)
        {
            foreach (var ip in inputParams.EnumerateArray())
            {
                if (ip.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String)
                {
                    inputParamName = n.GetString() ?? "input";
                    break;
                }
            }
        }

        var errorTolerance = VariableResolver.GetConfigString(batchNode.Config, "errorTolerance", "fail_fast")
            .Trim().ToLowerInvariant();
        var isBestEffort = errorTolerance == "best_effort";

        IReadOnlyList<JsonElement> items = Array.Empty<JsonElement>();
        if (variables.TryGetValue(inputParamName, out var arrayVar))
        {
            if (arrayVar.ValueKind == JsonValueKind.Array)
            {
                var list = new List<JsonElement>();
                foreach (var element in arrayVar.EnumerateArray()) list.Add(element);
                items = list;
            }
            else if (arrayVar.ValueKind == JsonValueKind.String)
            {
                try {
                    var parsed = JsonSerializer.Deserialize<JsonElement>(arrayVar.GetString() ?? "[]");
                    if (parsed.ValueKind == JsonValueKind.Array)
                    {
                        var list = new List<JsonElement>();
                        foreach (var element in parsed.EnumerateArray()) list.Add(element);
                        items = list;
                    }
                } catch {}
            }
        }

        var iterationVariableSnapshots = new List<Dictionary<string, JsonElement>>();
        var batchErrors = new List<string>();
        var semaphore = new SemaphoreSlim(concurrentSize);
        var currentIndex = 0;

        foreach (var chunk in items.Chunk(batchSize))
        {
            var tasks = chunk.Select(async item =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    int index = Interlocked.Increment(ref currentIndex) - 1;
                    var localVariables = new Dictionary<string, JsonElement>(variables, StringComparer.OrdinalIgnoreCase)
                    {
                        [$"{batchNodeKey}.locals.{inputParamName}"] = item,
                        [$"{batchNodeKey}.locals.index"] = JsonSerializer.SerializeToElement(index)
                    };

                    var fragmentResult = await ExecuteCanvasFragmentAsync(
                        tenantId,
                        workflowId,
                        executionId,
                        workflowCallStack,
                        batchNode.ChildCanvas,
                        localVariables,
                        eventChannel,
                        cancellationToken,
                        userId,
                        channelId,
                        isDebug,
                        databaseEnvironment);
                    
                    return (Result: fragmentResult, IterationVariables: localVariables);
                }
                finally
                {
                    semaphore.Release();
                }
            }).ToArray();

            var chunkResults = await Task.WhenAll(tasks);
            foreach (var chunkResult in chunkResults)
            {
                if (!chunkResult.Result.Success)
                {
                    if (!isBestEffort)
                    {
                        return chunkResult.Result;
                    }

                    lock (batchErrors)
                    {
                        batchErrors.Add(chunkResult.Result.ErrorMessage ?? "批处理子任务失败");
                    }
                }
                else
                {
                    lock (iterationVariableSnapshots)
                    {
                        iterationVariableSnapshots.Add(chunkResult.IterationVariables);
                    }
                }
            }
        }

        if (!isBestEffort && batchErrors.Count > 0)
        {
            return NodeRunResult.FailedResult(batchNodeKey, WorkflowNodeType.Batch,
                $"批处理失败（{batchErrors.Count} 项）: {batchErrors[0]}", InterruptType.None);
        }

        var outputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        
        if (batchNode.Config.TryGetValue("outputs", out var outputsArr) && outputsArr.ValueKind == JsonValueKind.Array)
        {
            foreach (var op in outputsArr.EnumerateArray())
            {
                if (!op.TryGetProperty("name", out var n) || n.ValueKind != JsonValueKind.String) continue;
                string outName = n.GetString() ?? "";
                
                if (!op.TryGetProperty("input", out var inProp) || inProp.ValueKind != JsonValueKind.Object) continue;
                if (!inProp.TryGetProperty("type", out var typeProp) || typeProp.ValueKind != JsonValueKind.String) continue;
                
                string type = typeProp.GetString() ?? "";
                if (type == "ref" && inProp.TryGetProperty("content", out var contentObj) &&
                    contentObj.TryGetProperty("keyPath", out var keyPathArr) && keyPathArr.ValueKind == JsonValueKind.Array)
                {
                    var pathSegments = keyPathArr.EnumerateArray().Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() : x.GetRawText()).Where(x => !string.IsNullOrWhiteSpace(x));
                    var refPath = string.Join(".", pathSegments);
                    
                    var aggregatedValues = new List<JsonElement>();
                    foreach (var iterationVars in iterationVariableSnapshots)
                    {
                        if (VariableResolver.TryResolvePath(iterationVars, refPath, out var val))
                        {
                            aggregatedValues.Add(val);
                        }
                    }
                    outputs[outName] = JsonSerializer.SerializeToElement(aggregatedValues);
                }
            }
        }

        outputs["batch_completed"] = JsonSerializer.SerializeToElement(true);
        outputs["batch_error_count"] = JsonSerializer.SerializeToElement(batchErrors.Count);
        if (batchErrors.Count > 0)
        {
            outputs["batch_errors"] = JsonSerializer.SerializeToElement(batchErrors);
        }

        return NodeRunResult.SuccessResult(batchNodeKey, WorkflowNodeType.Batch, outputs);
    }

    private static int? ExtractValueExpressionInt32(JsonElement expr)
    {
        if (expr.ValueKind != JsonValueKind.Object) return null;
        if (!expr.TryGetProperty("type", out var typeProp) || typeProp.ValueKind != JsonValueKind.String) return null;
        if (typeProp.GetString() != "literal") return null;
        if (!expr.TryGetProperty("content", out var contentProp)) return null;
        if (contentProp.ValueKind == JsonValueKind.Number && contentProp.TryGetInt32(out var i)) return i;
        if (contentProp.ValueKind == JsonValueKind.String && int.TryParse(contentProp.GetString(), out var i2)) return i2;
        return null;
    }

    private async Task<NodeRunResult> ExecuteCanvasFragmentAsync(
        TenantId tenantId,
        long workflowId,
        long executionId,
        IReadOnlyList<long> workflowCallStack,
        CanvasSchema canvas,
        Dictionary<string, JsonElement> variables,
        Channel<SseEvent>? eventChannel,
        CancellationToken cancellationToken,
        long? userId = null,
        string? channelId = null,
        bool isDebug = false,
        AiDatabaseRecordEnvironment databaseEnvironment = AiDatabaseRecordEnvironment.Draft)
    {
        var (nodeMap, topology) = GetOrCompileCanvas(canvas);
        var adjacency = topology.Adjacency;
        var predecessors = topology.Predecessors;
        var connectionsBySource = topology.ConnectionsBySource;
        var executionLevels = topology.ExecutionLevels;
        var skippedNodeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var persistedSkippedNodeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var outputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

        foreach (var level in executionLevels)
        {
            var newlySkippedByPredecessor = ResolveNodesWithAllSkippedPredecessors(level, predecessors, skippedNodeKeys);
            foreach (var nodeKey in newlySkippedByPredecessor)
            {
                skippedNodeKeys.Add(nodeKey);
            }
            await PersistSkippedNodesAsync(
                tenantId,
                executionId,
                level.Where(skippedNodeKeys.Contains),
                nodeMap,
                connectionsBySource,
                persistedSkippedNodeKeys,
                eventChannel,
                cancellationToken);

            var executableNodeKeys = level.Where(nodeKey => !skippedNodeKeys.Contains(nodeKey)).ToArray();
            if (executableNodeKeys.Length == 0)
            {
                continue;
            }

            var levelInput = new Dictionary<string, JsonElement>(variables, StringComparer.OrdinalIgnoreCase);
            var levelTasks = executableNodeKeys
                .Select(nodeKey => ExecuteNodeAsync(
                    tenantId,
                    workflowId,
                    executionId,
                    workflowCallStack,
                    nodeKey,
                    nodeMap,
                    connectionsBySource,
                    levelInput,
                    eventChannel,
                    cancellationToken,
                    userId,
                    channelId,
                    isDebug,
                    databaseEnvironment))
                .ToArray();
            var levelResults = await Task.WhenAll(levelTasks);
            var failedNode = levelResults.FirstOrDefault(x => !x.Success);
            if (failedNode is not null)
            {
                return failedNode;
            }

            foreach (var levelResult in levelResults)
            {
                MergeNodeOutputs(variables, levelResult);
                MergeNodeOutputs(outputs, levelResult);
            }

            foreach (var selectorNode in levelResults.Where(x => x.Success && x.NodeType == WorkflowNodeType.Selector))
            {
                var newlySkippedNodeKeys = new List<string>();
                foreach (var nodeKeyToSkip in ResolveSelectorBranchNodesToSkip(
                             selectorNode.NodeKey,
                             selectorNode.Outputs,
                             adjacency,
                             connectionsBySource))
                {
                    if (skippedNodeKeys.Add(nodeKeyToSkip))
                    {
                        newlySkippedNodeKeys.Add(nodeKeyToSkip);
                    }
                }

                await PersistSkippedNodesAsync(
                    tenantId,
                    executionId,
                    newlySkippedNodeKeys,
                    nodeMap,
                    connectionsBySource,
                    persistedSkippedNodeKeys,
                    eventChannel,
                    cancellationToken);
            }
        }

        return NodeRunResult.SuccessResult("fragment", null, outputs);
    }

    private async Task PersistSkippedNodesAsync(
        TenantId tenantId,
        long executionId,
        IEnumerable<string> nodeKeys,
        IReadOnlyDictionary<string, NodeSchema> nodeMap,
        IReadOnlyDictionary<string, List<ConnectionSchema>> connectionsBySource,
        ISet<string> persistedSkippedNodeKeys,
        Channel<SseEvent>? eventChannel,
        CancellationToken cancellationToken)
    {
        foreach (var nodeKey in nodeKeys.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!persistedSkippedNodeKeys.Add(nodeKey))
            {
                continue;
            }

            if (!nodeMap.TryGetValue(nodeKey, out var node))
            {
                continue;
            }

            var nodeExecution = new WorkflowNodeExecution(
                tenantId,
                executionId,
                nodeKey,
                node.Type,
                _idGenerator.NextId());
            nodeExecution.Skip("节点因分支语义传播被跳过。");
            await _nodeExecutionRepo.AddAsync(nodeExecution, cancellationToken);

            if (eventChannel is null)
            {
                continue;
            }

            await EmitEdgeStatusChangedAsync(
                executionId,
                nodeKey,
                EdgeExecutionStatus.Skipped,
                "branch_skipped",
                connectionsBySource,
                node.Type,
                null,
                eventChannel,
                cancellationToken);
            await eventChannel.Writer.WriteAsync(
                new SseEvent("node_skipped", JsonSerializer.Serialize(new
                {
                    executionId = executionId.ToString(),
                    nodeKey,
                    nodeType = node.Type.ToString(),
                    reason = "branch_skipped"
                })),
                cancellationToken);
        }
    }

    private async Task PersistBlockedByFailureAsync(
        TenantId tenantId,
        long executionId,
        string failedNodeKey,
        string reason,
        IReadOnlyDictionary<string, NodeSchema> nodeMap,
        IReadOnlyDictionary<string, List<string>> adjacency,
        IReadOnlyDictionary<string, List<ConnectionSchema>> connectionsBySource,
        IReadOnlySet<string> skippedNodeKeys,
        IReadOnlySet<string> preCompletedNodeKeySet,
        Channel<SseEvent>? eventChannel,
        CancellationToken cancellationToken)
    {
        var blockedNodeKeys = ResolveDownstreamNodesToBlock(
            failedNodeKey,
            adjacency,
            skippedNodeKeys,
            preCompletedNodeKeySet);
        if (blockedNodeKeys.Count == 0)
        {
            return;
        }

        foreach (var nodeKey in blockedNodeKeys)
        {
            if (!nodeMap.TryGetValue(nodeKey, out var node))
            {
                continue;
            }

            var nodeExecution = new WorkflowNodeExecution(
                tenantId,
                executionId,
                nodeKey,
                node.Type,
                _idGenerator.NextId());
            nodeExecution.Block(reason);
            await _nodeExecutionRepo.AddAsync(nodeExecution, cancellationToken);

            if (eventChannel is null)
            {
                continue;
            }

            await EmitEdgeStatusChangedAsync(
                executionId,
                nodeKey,
                EdgeExecutionStatus.Skipped,
                "upstream_failed",
                connectionsBySource,
                node.Type,
                null,
                eventChannel,
                cancellationToken);
            await eventChannel.Writer.WriteAsync(
                new SseEvent("node_blocked", JsonSerializer.Serialize(new
                {
                    executionId = executionId.ToString(),
                    nodeKey,
                    nodeType = node.Type.ToString(),
                    reason = "upstream_failed"
                })),
                cancellationToken);
        }
    }

    /// <summary>
    /// RT-05: 尝试通过 on_error 策略或 error 分支连接处理节点失败。
    /// 返回 true 表示已处理（可继续执行），false 表示需要按默认失败流程处理。
    /// </summary>
    private async Task<bool> TryHandleNodeErrorAsync(
        TenantId tenantId,
        WorkflowExecution execution,
        NodeRunResult failedNode,
        IReadOnlyDictionary<string, NodeSchema> nodeMap,
        IReadOnlyDictionary<string, List<ConnectionSchema>> connectionsBySource,
        Dictionary<string, JsonElement> variables,
        ISet<string> skippedNodeKeys,
        Channel<SseEvent>? eventChannel,
        CancellationToken cancellationToken)
    {
        // 检查节点 on_error 配置
        if (nodeMap.TryGetValue(failedNode.NodeKey, out var failedNodeSchema))
        {
            var onError = failedNodeSchema.Config.TryGetValue("on_error", out var onErrVal)
                ? VariableResolver.ToDisplayText(onErrVal).Trim().ToLowerInvariant()
                : "stop";

            if (onError == "continue")
            {
                // 忽略错误，以空输出继续执行
                _logger.LogWarning(
                    "节点 {NodeKey} 失败，on_error=continue，跳过错误继续执行。错误: {Error}",
                    failedNode.NodeKey,
                    failedNode.ErrorMessage);
                variables[$"{failedNode.NodeKey}.error"] = JsonSerializer.SerializeToElement(failedNode.ErrorMessage ?? "");
                return true;
            }
        }

        // 检查 error 分支连接（SourcePort == "error"）
        if (!connectionsBySource.TryGetValue(failedNode.NodeKey, out var outgoingConns))
        {
            return false;
        }

        var errorBranchTargets = outgoingConns
            .Where(c => string.Equals(c.SourcePort, "error", StringComparison.OrdinalIgnoreCase))
            .Select(c => c.TargetNodeKey)
            .Where(k => !skippedNodeKeys.Contains(k))
            .ToList();

        if (errorBranchTargets.Count == 0)
        {
            return false;
        }

        // 将 error 信息注入变量供下游消费
        variables["error_message"] = JsonSerializer.SerializeToElement(failedNode.ErrorMessage ?? "");
        variables["error_node_key"] = JsonSerializer.SerializeToElement(failedNode.NodeKey);
        _logger.LogWarning(
            "节点 {NodeKey} 失败，路由到 error 分支: [{Targets}]。",
            failedNode.NodeKey,
            string.Join(", ", errorBranchTargets));

        if (eventChannel is not null)
        {
            await eventChannel.Writer.WriteAsync(
                new SseEvent("branch_decision", JsonSerializer.Serialize(new
                {
                    executionId = execution.Id.ToString(),
                    nodeKey = failedNode.NodeKey,
                    selectedBranch = "error",
                    candidates = new[] { "error" }
                })),
                cancellationToken);
        }

        return true;
    }

    private static async Task EmitEdgeStatusChangedAsync(
        long executionId,
        string nodeKey,
        EdgeExecutionStatus status,
        string? reason,
        IReadOnlyDictionary<string, List<ConnectionSchema>> connectionsBySource,
        WorkflowNodeType? nodeType,
        IReadOnlyDictionary<string, JsonElement>? outputs,
        Channel<SseEvent>? eventChannel,
        CancellationToken cancellationToken)
    {
        if (eventChannel is null ||
            !connectionsBySource.TryGetValue(nodeKey, out var outgoingConnections) ||
            outgoingConnections.Count == 0)
        {
            return;
        }

        foreach (var connection in outgoingConnections)
        {
            var edgeStatus = status;
            var edgeReason = reason;
            if (status == EdgeExecutionStatus.Success &&
                nodeType == WorkflowNodeType.Selector &&
                outputs is not null)
            {
                var selectedBranch = ResolveSelectedSelectorBranch(outputs);
                if (selectedBranch == SelectorBranch.True && IsSelectorFalseConnection(connection))
                {
                    edgeStatus = EdgeExecutionStatus.Skipped;
                    edgeReason = "selector_unselected_branch";
                }
                else if (selectedBranch == SelectorBranch.False && IsSelectorTrueConnection(connection))
                {
                    edgeStatus = EdgeExecutionStatus.Skipped;
                    edgeReason = "selector_unselected_branch";
                }
            }

            await eventChannel.Writer.WriteAsync(
                new SseEvent("edge_status_changed", JsonSerializer.Serialize(new
                {
                    executionId = executionId.ToString(),
                    edge = new
                    {
                        sourceNodeKey = connection.SourceNodeKey,
                        sourcePort = connection.SourcePort,
                        targetNodeKey = connection.TargetNodeKey,
                        targetPort = connection.TargetPort,
                        status = (int)edgeStatus,
                        reason = edgeReason
                    }
                })),
                cancellationToken);
        }
    }

    private static bool HasControlSignal(Dictionary<string, JsonElement> variables, string key)
    {
        if (!variables.TryGetValue(key, out var signal))
        {
            return false;
        }

        return VariableResolver.TryGetBoolean(signal, out var boolSignal)
            ? boolSignal
            : string.Equals(VariableResolver.ToDisplayText(signal), "1", StringComparison.OrdinalIgnoreCase);
    }

    private static void ClearControlSignal(Dictionary<string, JsonElement> variables, string key)
    {
        if (variables.ContainsKey(key))
        {
            variables.Remove(key);
        }
    }

    private static IReadOnlyList<JsonElement> ResolveBatchItems(
        Dictionary<string, JsonElement> variables,
        string inputArrayPath)
    {
        if (!string.IsNullOrWhiteSpace(inputArrayPath) &&
            VariableResolver.TryResolvePath(variables, inputArrayPath, out var source) &&
            source.ValueKind == JsonValueKind.Array)
        {
            return source.EnumerateArray().Select(x => x.Clone()).ToArray();
        }

        return [JsonSerializer.SerializeToElement<object?>(null)];
    }

    private static IEnumerable<IReadOnlyList<JsonElement>> Chunk(IReadOnlyList<JsonElement> items, int size)
    {
        if (items.Count == 0)
        {
            yield break;
        }

        for (var i = 0; i < items.Count; i += size)
        {
            var count = Math.Min(size, items.Count - i);
            var chunk = new List<JsonElement>(count);
            for (var j = 0; j < count; j++)
            {
                chunk.Add(items[i + j]);
            }

            yield return chunk;
        }
    }

    private static IReadOnlyList<string> ResolveSelectorBranchNodesToSkip(
        string selectorNodeKey,
        Dictionary<string, JsonElement> outputs,
        Dictionary<string, List<string>> adjacency,
        Dictionary<string, List<ConnectionSchema>> connectionsBySource)
    {
        if (!connectionsBySource.TryGetValue(selectorNodeKey, out var outgoingConnections) || outgoingConnections.Count == 0)
        {
            return Array.Empty<string>();
        }

        var selectedBranch = ResolveSelectedSelectorBranch(outputs);
        if (selectedBranch is null)
        {
            return Array.Empty<string>();
        }

        var trueBranchStarts = outgoingConnections
            .Where(IsSelectorTrueConnection)
            .Select(x => x.TargetNodeKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var falseBranchStarts = outgoingConnections
            .Where(IsSelectorFalseConnection)
            .Select(x => x.TargetNodeKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // 兜底：如果未携带条件/端口信息但只有 2 条输出，按连线顺序映射 true/false。
        if (trueBranchStarts.Count == 0 && falseBranchStarts.Count == 0 && outgoingConnections.Count == 2)
        {
            trueBranchStarts.Add(outgoingConnections[0].TargetNodeKey);
            falseBranchStarts.Add(outgoingConnections[1].TargetNodeKey);
        }

        var selectedStarts = selectedBranch == SelectorBranch.True ? trueBranchStarts : falseBranchStarts;
        var unselectedStarts = selectedBranch == SelectorBranch.True ? falseBranchStarts : trueBranchStarts;
        if (selectedStarts.Count == 0 || unselectedStarts.Count == 0)
        {
            return Array.Empty<string>();
        }

        var selectedNodes = TraverseReachableNodes(selectedStarts, adjacency);
        var unselectedNodes = TraverseReachableNodes(unselectedStarts, adjacency);
        unselectedNodes.ExceptWith(selectedNodes);
        unselectedNodes.Remove(selectorNodeKey);
        return unselectedNodes.ToList();
    }

    private static SelectorBranch? ResolveSelectedSelectorBranch(IReadOnlyDictionary<string, JsonElement> outputs)
    {
        if (outputs.TryGetValue("selected_branch", out var selectedBranchRaw))
        {
            var selectedBranchText = VariableResolver.ToDisplayText(selectedBranchRaw);
            if (selectedBranchText.Contains("true", StringComparison.OrdinalIgnoreCase))
            {
                return SelectorBranch.True;
            }

            if (selectedBranchText.Contains("false", StringComparison.OrdinalIgnoreCase))
            {
                return SelectorBranch.False;
            }
        }

        if (outputs.TryGetValue("selector_result", out var selectorResultRaw))
        {
            if (VariableResolver.TryGetBoolean(selectorResultRaw, out var boolResult))
            {
                return boolResult ? SelectorBranch.True : SelectorBranch.False;
            }

            var selectorResultText = VariableResolver.ToDisplayText(selectorResultRaw);
            if (string.Equals(selectorResultText, "1", StringComparison.OrdinalIgnoreCase))
            {
                return SelectorBranch.True;
            }

            if (string.Equals(selectorResultText, "0", StringComparison.OrdinalIgnoreCase))
            {
                return SelectorBranch.False;
            }
        }

        return null;
    }

    private static bool IsSelectorTrueConnection(ConnectionSchema connection)
    {
        var condition = connection.Condition ?? string.Empty;
        if (condition.Contains("selector_result", StringComparison.OrdinalIgnoreCase) &&
            condition.Contains("true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (condition.Contains("selected_branch", StringComparison.OrdinalIgnoreCase) &&
            condition.Contains("true_branch", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(condition, "true", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(condition, "1", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return connection.SourcePort.Contains("true", StringComparison.OrdinalIgnoreCase) ||
               connection.TargetPort.Contains("true", StringComparison.OrdinalIgnoreCase) ||
               connection.SourcePort.Contains("yes", StringComparison.OrdinalIgnoreCase) ||
               connection.TargetPort.Contains("yes", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSelectorFalseConnection(ConnectionSchema connection)
    {
        var condition = connection.Condition ?? string.Empty;
        if (condition.Contains("selector_result", StringComparison.OrdinalIgnoreCase) &&
            condition.Contains("false", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (condition.Contains("selected_branch", StringComparison.OrdinalIgnoreCase) &&
            condition.Contains("false_branch", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(condition, "false", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(condition, "0", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return connection.SourcePort.Contains("false", StringComparison.OrdinalIgnoreCase) ||
               connection.TargetPort.Contains("false", StringComparison.OrdinalIgnoreCase) ||
               connection.SourcePort.Contains("no", StringComparison.OrdinalIgnoreCase) ||
               connection.TargetPort.Contains("no", StringComparison.OrdinalIgnoreCase);
    }

    private static List<string> ResolveLoopBodyNodeKeys(
        NodeSchema loopNode,
        Dictionary<string, List<string>> adjacency,
        Dictionary<string, List<ConnectionSchema>> connectionsBySource)
    {
        var bodyNodeKeysConfig = VariableResolver.GetConfigString(loopNode.Config, "bodyNodeKeys");
        if (!string.IsNullOrWhiteSpace(bodyNodeKeysConfig))
        {
            return bodyNodeKeysConfig
                .Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        if (!connectionsBySource.TryGetValue(loopNode.Key, out var outgoingConnections) || outgoingConnections.Count == 0)
        {
            return new List<string>();
        }

        var bodyStarts = outgoingConnections
            .Where(IsLoopContinueConnection)
            .Select(x => x.TargetNodeKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var exitStarts = outgoingConnections
            .Where(IsLoopExitConnection)
            .Select(x => x.TargetNodeKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (bodyStarts.Count == 0 && outgoingConnections.Count == 1)
        {
            bodyStarts.Add(outgoingConnections[0].TargetNodeKey);
        }

        if (bodyStarts.Count == 0)
        {
            return new List<string>();
        }

        var bodyNodes = TraverseReachableNodes(bodyStarts, adjacency);
        if (exitStarts.Count > 0)
        {
            var exitNodes = TraverseReachableNodes(exitStarts, adjacency);
            bodyNodes.ExceptWith(exitNodes);
        }

        bodyNodes.Remove(loopNode.Key);
        return bodyNodes.ToList();
    }

    private static bool IsLoopContinueConnection(ConnectionSchema connection)
    {
        var condition = connection.Condition ?? string.Empty;
        if (condition.Contains("loop_completed", StringComparison.OrdinalIgnoreCase) &&
            condition.Contains("false", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(condition, "false", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(condition, "0", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return connection.SourcePort.Contains("continue", StringComparison.OrdinalIgnoreCase) ||
               connection.TargetPort.Contains("continue", StringComparison.OrdinalIgnoreCase) ||
               connection.SourcePort.Contains("loop_body", StringComparison.OrdinalIgnoreCase) ||
               connection.TargetPort.Contains("loop_body", StringComparison.OrdinalIgnoreCase) ||
               connection.SourcePort.Contains("body", StringComparison.OrdinalIgnoreCase) ||
               connection.TargetPort.Contains("body", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLoopExitConnection(ConnectionSchema connection)
    {
        var condition = connection.Condition ?? string.Empty;
        if (condition.Contains("loop_completed", StringComparison.OrdinalIgnoreCase) &&
            condition.Contains("true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(condition, "true", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(condition, "1", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return connection.SourcePort.Contains("exit", StringComparison.OrdinalIgnoreCase) ||
               connection.TargetPort.Contains("exit", StringComparison.OrdinalIgnoreCase) ||
               connection.SourcePort.Contains("done", StringComparison.OrdinalIgnoreCase) ||
               connection.TargetPort.Contains("done", StringComparison.OrdinalIgnoreCase) ||
               connection.SourcePort.Contains("completed", StringComparison.OrdinalIgnoreCase) ||
               connection.TargetPort.Contains("completed", StringComparison.OrdinalIgnoreCase);
    }

    private static HashSet<string> TraverseReachableNodes(
        IEnumerable<string> starts,
        IReadOnlyDictionary<string, List<string>> adjacency)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<(string NodeKey, int Depth)>(
            starts
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => (x, 0)));

        while (queue.Count > 0)
        {
            var (current, depth) = queue.Dequeue();
            if (depth > MaxGraphTraversalDepth)
            {
                throw new InvalidOperationException(
                    $"图传播深度超过限制({MaxGraphTraversalDepth})，请检查是否存在异常超深链路。");
            }

            if (!visited.Add(current))
            {
                continue;
            }

            if (!adjacency.TryGetValue(current, out var targets))
            {
                continue;
            }

            foreach (var target in targets)
            {
                queue.Enqueue((target, depth + 1));
            }
        }

        return visited;
    }

    private static TimeSpan? ResolveNodeTimeout(NodeSchema node)
    {
        var timeoutMs = VariableResolver.GetConfigInt32(node.Config, "timeoutMs", 0);
        if (timeoutMs > 0)
        {
            return TimeSpan.FromMilliseconds(Math.Clamp(timeoutMs, 50, 600_000));
        }

        return node.Type switch
        {
            WorkflowNodeType.Llm => TimeSpan.FromSeconds(60),
            WorkflowNodeType.HttpRequester => TimeSpan.FromSeconds(30),
            WorkflowNodeType.CodeRunner => TimeSpan.FromSeconds(30),
            _ => null
        };
    }

    private static IReadOnlyList<string> ResolveNodesWithAllSkippedPredecessors(
        IReadOnlyList<string> levelNodes,
        IReadOnlyDictionary<string, List<string>> predecessors,
        IReadOnlySet<string> skippedNodeKeys)
    {
        var result = new List<string>();
        foreach (var nodeKey in levelNodes)
        {
            if (!predecessors.TryGetValue(nodeKey, out var nodePredecessors) || nodePredecessors.Count == 0)
            {
                continue;
            }

            if (nodePredecessors.All(skippedNodeKeys.Contains))
            {
                result.Add(nodeKey);
            }
        }

        return result;
    }

    private static IReadOnlyList<string> ResolveDownstreamNodesToBlock(
        string failedNodeKey,
        IReadOnlyDictionary<string, List<string>> adjacency,
        IReadOnlySet<string> skippedNodeKeys,
        IReadOnlySet<string> preCompletedNodeKeySet)
    {
        var reachable = TraverseReachableNodes(new[] { failedNodeKey }, adjacency);
        reachable.Remove(failedNodeKey);
        foreach (var skippedNodeKey in skippedNodeKeys)
        {
            reachable.Remove(skippedNodeKey);
        }
        foreach (var preCompletedNodeKey in preCompletedNodeKeySet)
        {
            reachable.Remove(preCompletedNodeKey);
        }

        return reachable.ToList();
    }

    private static List<List<string>> TopologicalSortSubsetByLevels(
        IReadOnlyList<string> subsetNodes,
        Dictionary<string, List<string>> adjacency)
    {
        var subset = new HashSet<string>(subsetNodes, StringComparer.OrdinalIgnoreCase);
        var indegree = subset.ToDictionary(key => key, _ => 0, StringComparer.OrdinalIgnoreCase);

        foreach (var source in subset)
        {
            if (!adjacency.TryGetValue(source, out var targets))
            {
                continue;
            }

            foreach (var target in targets)
            {
                if (subset.Contains(target))
                {
                    indegree[target]++;
                }
            }
        }

        var queue = new Queue<string>(indegree.Where(x => x.Value == 0).Select(x => x.Key));
        var levels = new List<List<string>>();
        var visitedCount = 0;

        while (queue.Count > 0)
        {
            var currentLevelCount = queue.Count;
            var currentLevel = new List<string>(currentLevelCount);

            for (var i = 0; i < currentLevelCount; i++)
            {
                var key = queue.Dequeue();
                currentLevel.Add(key);
                visitedCount++;

                if (!adjacency.TryGetValue(key, out var targets))
                {
                    continue;
                }

                foreach (var target in targets)
                {
                    if (!subset.Contains(target))
                    {
                        continue;
                    }

                    indegree[target]--;
                    if (indegree[target] == 0)
                    {
                        queue.Enqueue(target);
                    }
                }
            }

            levels.Add(currentLevel);
        }

        if (visitedCount != subset.Count)
        {
            var cycleNodes = indegree
                .Where(x => x.Value > 0)
                .Select(x => x.Key)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            throw new InvalidOperationException(
                $"循环子图存在环路，涉及节点: {string.Join(", ", cycleNodes)}。");
        }

        return levels;
    }

    private static List<List<string>> TopologicalSortByLevels(
        IReadOnlyList<NodeSchema> nodes,
        Dictionary<string, List<string>> adjacency)
    {
        var indegree = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in nodes)
        {
            indegree.TryAdd(node.Key, 0);
        }

        foreach (var targets in adjacency.Values)
        {
            foreach (var target in targets)
            {
                if (indegree.ContainsKey(target))
                {
                    indegree[target]++;
                }
            }
        }

        var queue = new Queue<string>(indegree.Where(x => x.Value == 0).Select(x => x.Key));
        var levels = new List<List<string>>();
        var visitedCount = 0;

        while (queue.Count > 0)
        {
            var currentLevelCount = queue.Count;
            var currentLevel = new List<string>(currentLevelCount);

            for (var i = 0; i < currentLevelCount; i++)
            {
                var key = queue.Dequeue();
                currentLevel.Add(key);
                visitedCount++;

                if (!adjacency.TryGetValue(key, out var targets))
                {
                    continue;
                }

                foreach (var target in targets)
                {
                    if (!indegree.ContainsKey(target))
                    {
                        continue;
                    }

                    indegree[target]--;
                    if (indegree[target] == 0)
                    {
                        queue.Enqueue(target);
                    }
                }
            }

            levels.Add(currentLevel);
        }

        if (visitedCount != nodes.Count)
        {
            var cycleNodes = indegree
                .Where(x => x.Value > 0)
                .Select(x => x.Key)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            throw new InvalidOperationException(
                $"检测到工作流环路，涉及节点: {string.Join(", ", cycleNodes)}。请移除循环依赖后重试。");
        }

        return levels;
    }

    private static void MergeNodeOutputs(
        IDictionary<string, JsonElement> variables,
        NodeRunResult result)
    {
        foreach (var kvp in result.Outputs)
        {
            variables[kvp.Key] = kvp.Value;
            if (string.IsNullOrWhiteSpace(result.NodeKey))
            {
                continue;
            }

            variables[$"{result.NodeKey}.{kvp.Key}"] = kvp.Value;
            variables[$"block_output_{result.NodeKey}.{kvp.Key}"] = kvp.Value;
        }
    }

    private static readonly Dictionary<string, JsonElement> EmptyOutputs = new(StringComparer.OrdinalIgnoreCase);

    private enum SelectorBranch
    {
        True = 1,
        False = 2
    }

    private sealed record NodeRunResult(
        string NodeKey,
        WorkflowNodeType? NodeType,
        bool Success,
        Dictionary<string, JsonElement> Outputs,
        string? ErrorMessage,
        InterruptType InterruptType)
    {
        public static NodeRunResult SuccessResult(string nodeKey, WorkflowNodeType? nodeType, Dictionary<string, JsonElement> outputs)
            => new(nodeKey, nodeType, true, outputs, null, InterruptType.None);

        public static NodeRunResult FailedResult(string nodeKey, WorkflowNodeType? nodeType, string? errorMessage, InterruptType interruptType)
            => new(nodeKey, nodeType, false, EmptyOutputs, errorMessage, interruptType);
    }

    private sealed record LoopIterationResult(
        string NodeKey,
        bool Success,
        string? ErrorMessage,
        InterruptType InterruptType,
        IReadOnlyList<string> BodyNodeKeysToSkip)
    {
        public static LoopIterationResult Succeeded(string nodeKey, IReadOnlyList<string> bodyNodeKeysToSkip)
            => new(nodeKey, true, null, InterruptType.None, bodyNodeKeysToSkip);

        public static LoopIterationResult Failed(string nodeKey, string? errorMessage, InterruptType interruptType)
            => new(nodeKey, false, errorMessage, interruptType, Array.Empty<string>());
    }

    // RT-07: 编译后的画布拓扑数据缓存条目（不含 NodeMap，以免不同配置的同结构画布污染缓存）。
    private sealed record CompiledCanvas(
        Dictionary<string, List<string>> Adjacency,
        Dictionary<string, List<string>> Predecessors,
        Dictionary<string, List<ConnectionSchema>> ConnectionsBySource,
        List<List<string>> ExecutionLevels);
}
