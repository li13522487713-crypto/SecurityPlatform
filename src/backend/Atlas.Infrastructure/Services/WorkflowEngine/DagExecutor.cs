using System.Diagnostics;
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
        IReadOnlySet<string>? preCompletedNodeKeys = null)
    {
        execution.Start();
        await _executionRepo.UpdateAsync(execution, cancellationToken);

        var variables = new Dictionary<string, JsonElement>(inputs, StringComparer.OrdinalIgnoreCase);
        var currentCallStack = workflowCallStack is not null && workflowCallStack.Count > 0
            ? workflowCallStack
            : new[] { execution.WorkflowId };

        try
        {
            // 构建邻接表
            var nodeMap = canvas.Nodes.ToDictionary(n => n.Key, n => n, StringComparer.OrdinalIgnoreCase);
            var adjacency = BuildAdjacency(canvas);
            var connectionsBySource = BuildConnectionsBySource(canvas);
            var executionLevels = TopologicalSortByLevels(canvas.Nodes, adjacency);
            var skippedNodeKeys = preCompletedNodeKeys is null
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(preCompletedNodeKeys, StringComparer.OrdinalIgnoreCase);

            foreach (var level in executionLevels)
            {
                cancellationToken.ThrowIfCancellationRequested();
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
                        levelInput,
                        eventChannel,
                        cancellationToken))
                    .ToArray();

                var levelResults = await Task.WhenAll(levelTasks);
                var failedNode = levelResults.FirstOrDefault(x => !x.Success);
                if (failedNode is not null)
                {
                    if (failedNode.InterruptType != InterruptType.None)
                    {
                        execution.Interrupt(failedNode.InterruptType, failedNode.NodeKey);
                        await _executionRepo.UpdateAsync(execution, cancellationToken);
                        return;
                    }

                    execution.Fail(failedNode.ErrorMessage ?? "节点执行失败");
                    await _executionRepo.UpdateAsync(execution, cancellationToken);
                    return;
                }

                // 同层全部成功后再合并输出，避免失败层产生部分提交。
                foreach (var result in levelResults)
                {
                    foreach (var kvp in result.Outputs)
                    {
                        variables[kvp.Key] = kvp.Value;
                    }
                }

                // 条件分支节点执行完成后，按选中分支跳过未命中的下游节点。
                foreach (var selectorNode in levelResults.Where(x => x.Success && x.NodeType == WorkflowNodeType.Selector))
                {
                    foreach (var nodeKeyToSkip in ResolveSelectorBranchNodesToSkip(
                                 selectorNode.NodeKey,
                                 selectorNode.Outputs,
                                 adjacency,
                                 connectionsBySource))
                    {
                        skippedNodeKeys.Add(nodeKeyToSkip);
                    }
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
                        cancellationToken);

                    if (!loopResult.Success)
                    {
                        if (loopResult.InterruptType != InterruptType.None)
                        {
                            execution.Interrupt(loopResult.InterruptType, loopResult.NodeKey);
                            await _executionRepo.UpdateAsync(execution, cancellationToken);
                            return;
                        }

                        execution.Fail(loopResult.ErrorMessage ?? "循环节点执行失败");
                        await _executionRepo.UpdateAsync(execution, cancellationToken);
                        return;
                    }

                    foreach (var bodyNodeKey in loopResult.BodyNodeKeysToSkip)
                    {
                        skippedNodeKeys.Add(bodyNodeKey);
                    }
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
                        cancellationToken);

                    if (!batchResult.Success)
                    {
                        if (batchResult.InterruptType != InterruptType.None)
                        {
                            execution.Interrupt(batchResult.InterruptType, batchResult.NodeKey);
                            await _executionRepo.UpdateAsync(execution, cancellationToken);
                            return;
                        }

                        execution.Fail(batchResult.ErrorMessage ?? "批处理节点执行失败");
                        await _executionRepo.UpdateAsync(execution, cancellationToken);
                        return;
                    }

                    foreach (var kvp in batchResult.Outputs)
                    {
                        variables[kvp.Key] = kvp.Value;
                    }
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

            execution.Complete(JsonSerializer.Serialize(variables));
            await _executionRepo.UpdateAsync(execution, cancellationToken);
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
        if (string.IsNullOrWhiteSpace(canvasJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<CanvasSchema>(canvasJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            return null;
        }
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
        Dictionary<string, JsonElement> inputVariables,
        Channel<SseEvent>? eventChannel,
        CancellationToken cancellationToken)
    {
        if (!nodeMap.TryGetValue(nodeKey, out var node))
        {
            return NodeRunResult.SuccessResult(nodeKey, null, EmptyOutputs);
        }

        var executor = _registry.GetExecutor(node.Type);
        if (executor is null)
        {
            _logger.LogWarning("未找到节点类型 {NodeType} 的执行器，跳过节点 {NodeKey}", node.Type, nodeKey);
            return NodeRunResult.SuccessResult(nodeKey, node.Type, EmptyOutputs);
        }

        var nodeExec = new WorkflowNodeExecution(tenantId, executionId, nodeKey, node.Type, _idGenerator.NextId());
        nodeExec.Start(JsonSerializer.Serialize(inputVariables));
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
        }

        var sw = Stopwatch.StartNew();
        var context = new NodeExecutionContext(
            node,
            inputVariables,
            _serviceProvider,
            tenantId,
            workflowId,
            executionId,
            workflowCallStack,
            eventChannel);

        try
        {
            var result = await executor.ExecuteAsync(context, cancellationToken);
            sw.Stop();

            if (result.Success)
            {
                nodeExec.Complete(JsonSerializer.Serialize(result.Outputs), sw.ElapsedMilliseconds);
                await _nodeExecutionRepo.UpdateAsync(nodeExec, cancellationToken);

                if (eventChannel is not null)
                {
                    await eventChannel.Writer.WriteAsync(
                        new SseEvent("node_output", JsonSerializer.Serialize(new
                        {
                            executionId = executionId.ToString(),
                            nodeKey,
                            nodeType = node.Type.ToString(),
                            outputs = result.Outputs
                        })),
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

            nodeExec.Fail(result.ErrorMessage ?? "节点执行失败");
            await _nodeExecutionRepo.UpdateAsync(nodeExec, cancellationToken);
            if (eventChannel is not null)
            {
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
        CancellationToken cancellationToken)
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
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var bodyLevel in bodyLevels)
            {
                var levelInput = new Dictionary<string, JsonElement>(variables, StringComparer.OrdinalIgnoreCase);
                var bodyTasks = bodyLevel
                    .Select(nodeKey => ExecuteNodeAsync(
                        tenantId,
                        workflowId,
                        executionId,
                        workflowCallStack,
                        nodeKey,
                        nodeMap,
                        levelInput,
                        eventChannel,
                        cancellationToken))
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
                    foreach (var kvp in bodyResult.Outputs)
                    {
                        variables[kvp.Key] = kvp.Value;
                    }
                }

                if (HasControlSignal(variables, "loop_break"))
                {
                    variables["loop_completed"] = JsonSerializer.SerializeToElement(true);
                    ClearControlSignal(variables, "loop_break");
                    ClearControlSignal(variables, "loop_continue");
                    return LoopIterationResult.Succeeded(loopNodeKey, bodyNodeKeys);
                }

                if (HasControlSignal(variables, "loop_continue"))
                {
                    ClearControlSignal(variables, "loop_continue");
                    goto NextLoopIteration;
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
                loopInput,
                eventChannel,
                cancellationToken);
            if (!loopResult.Success)
            {
                return LoopIterationResult.Failed(loopResult.NodeKey, loopResult.ErrorMessage, loopResult.InterruptType);
            }

            foreach (var kvp in loopResult.Outputs)
            {
                variables[kvp.Key] = kvp.Value;
            }

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
        CancellationToken cancellationToken)
    {
        if (!nodeMap.TryGetValue(batchNodeKey, out var batchNode))
        {
            return NodeRunResult.SuccessResult(batchNodeKey, WorkflowNodeType.Batch, EmptyOutputs);
        }

        if (batchNode.ChildCanvas is null || batchNode.ChildCanvas.Nodes.Count == 0)
        {
            return NodeRunResult.SuccessResult(batchNodeKey, WorkflowNodeType.Batch, EmptyOutputs);
        }

        var concurrentSize = Math.Clamp(VariableResolver.GetConfigInt32(batchNode.Config, "concurrentSize", 4), 1, 64);
        var batchSize = Math.Clamp(VariableResolver.GetConfigInt32(batchNode.Config, "batchSize", 1), 1, 10_000);
        var inputArrayPath = VariableResolver.GetConfigString(batchNode.Config, "inputArrayPath");
        var itemVariable = VariableResolver.GetConfigString(batchNode.Config, "itemVariable", "batch_item");
        var itemIndexVariable = VariableResolver.GetConfigString(batchNode.Config, "itemIndexVariable", "batch_item_index");
        var outputKey = VariableResolver.GetConfigString(batchNode.Config, "outputKey", "batch_results");

        var items = ResolveBatchItems(variables, inputArrayPath);
        var aggregatedResults = new List<JsonElement>();
        var semaphore = new SemaphoreSlim(concurrentSize);
        var currentIndex = 0;

        foreach (var chunk in Chunk(items, batchSize))
        {
            var tasks = chunk.Select(async item =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var localVariables = new Dictionary<string, JsonElement>(variables, StringComparer.OrdinalIgnoreCase)
                    {
                        [itemVariable] = item,
                        [itemIndexVariable] = JsonSerializer.SerializeToElement(Interlocked.Increment(ref currentIndex) - 1)
                    };

                    var fragmentResult = await ExecuteCanvasFragmentAsync(
                        tenantId,
                        workflowId,
                        executionId,
                        workflowCallStack,
                        batchNode.ChildCanvas,
                        localVariables,
                        eventChannel,
                        cancellationToken);
                    if (!fragmentResult.Success)
                    {
                        return fragmentResult;
                    }

                    lock (aggregatedResults)
                    {
                        aggregatedResults.Add(JsonSerializer.SerializeToElement(fragmentResult.Outputs));
                    }

                    return NodeRunResult.SuccessResult(batchNodeKey, WorkflowNodeType.Batch, fragmentResult.Outputs);
                }
                finally
                {
                    semaphore.Release();
                }
            }).ToArray();

            var chunkResults = await Task.WhenAll(tasks);
            var failed = chunkResults.FirstOrDefault(x => !x.Success);
            if (failed is not null)
            {
                return failed;
            }
        }

        var outputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
        {
            [outputKey] = JsonSerializer.SerializeToElement(aggregatedResults),
            ["batch_completed"] = JsonSerializer.SerializeToElement(true)
        };
        return NodeRunResult.SuccessResult(batchNodeKey, WorkflowNodeType.Batch, outputs);
    }

    private async Task<NodeRunResult> ExecuteCanvasFragmentAsync(
        TenantId tenantId,
        long workflowId,
        long executionId,
        IReadOnlyList<long> workflowCallStack,
        CanvasSchema canvas,
        Dictionary<string, JsonElement> variables,
        Channel<SseEvent>? eventChannel,
        CancellationToken cancellationToken)
    {
        var nodeMap = canvas.Nodes.ToDictionary(n => n.Key, n => n, StringComparer.OrdinalIgnoreCase);
        var adjacency = BuildAdjacency(canvas);
        var connectionsBySource = BuildConnectionsBySource(canvas);
        var executionLevels = TopologicalSortByLevels(canvas.Nodes, adjacency);
        var skippedNodeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var outputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

        foreach (var level in executionLevels)
        {
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
                    levelInput,
                    eventChannel,
                    cancellationToken))
                .ToArray();
            var levelResults = await Task.WhenAll(levelTasks);
            var failedNode = levelResults.FirstOrDefault(x => !x.Success);
            if (failedNode is not null)
            {
                return failedNode;
            }

            foreach (var levelResult in levelResults)
            {
                foreach (var kvp in levelResult.Outputs)
                {
                    variables[kvp.Key] = kvp.Value;
                    outputs[kvp.Key] = kvp.Value;
                }
            }

            foreach (var selectorNode in levelResults.Where(x => x.Success && x.NodeType == WorkflowNodeType.Selector))
            {
                foreach (var nodeKeyToSkip in ResolveSelectorBranchNodesToSkip(
                             selectorNode.NodeKey,
                             selectorNode.Outputs,
                             adjacency,
                             connectionsBySource))
                {
                    skippedNodeKeys.Add(nodeKeyToSkip);
                }
            }
        }

        return NodeRunResult.SuccessResult("fragment", null, outputs);
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

    private static SelectorBranch? ResolveSelectedSelectorBranch(Dictionary<string, JsonElement> outputs)
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
        Dictionary<string, List<string>> adjacency)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>(starts);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
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
                queue.Enqueue(target);
            }
        }

        return visited;
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
}
