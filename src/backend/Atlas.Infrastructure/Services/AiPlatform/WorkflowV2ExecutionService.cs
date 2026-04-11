using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Application.AiPlatform.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.AiPlatform.Enums;
using Atlas.Infrastructure.Services;
using Atlas.Infrastructure.Services.WorkflowEngine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class WorkflowV2ExecutionService : IWorkflowV2ExecutionService
{
    private readonly IWorkflowMetaRepository _metaRepo;
    private readonly IWorkflowDraftRepository _draftRepo;
    private readonly IWorkflowVersionRepository _versionRepo;
    private readonly IWorkflowExecutionRepository _executionRepo;
    private readonly IWorkflowNodeExecutionRepository _nodeExecutionRepo;
    private readonly DagExecutor _dagExecutor;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly WorkflowExecutionCancellationRegistry _cancellationRegistry;
    private readonly IIdGeneratorAccessor _idGenerator;
    private readonly IAppContextAccessor _appContextAccessor;
    private readonly ILogger<WorkflowV2ExecutionService> _logger;

    public WorkflowV2ExecutionService(
        IWorkflowMetaRepository metaRepo,
        IWorkflowDraftRepository draftRepo,
        IWorkflowVersionRepository versionRepo,
        IWorkflowExecutionRepository executionRepo,
        IWorkflowNodeExecutionRepository nodeExecutionRepo,
        DagExecutor dagExecutor,
        IServiceScopeFactory scopeFactory,
        WorkflowExecutionCancellationRegistry cancellationRegistry,
        IIdGeneratorAccessor idGenerator,
        IAppContextAccessor appContextAccessor,
        ILogger<WorkflowV2ExecutionService> logger)
    {
        _metaRepo = metaRepo;
        _draftRepo = draftRepo;
        _versionRepo = versionRepo;
        _executionRepo = executionRepo;
        _nodeExecutionRepo = nodeExecutionRepo;
        _dagExecutor = dagExecutor;
        _scopeFactory = scopeFactory;
        _cancellationRegistry = cancellationRegistry;
        _idGenerator = idGenerator;
        _appContextAccessor = appContextAccessor;
        _logger = logger;
        _logger.LogDebug("WorkflowV2ExecutionService initialized.");
    }

    public WorkflowV2ExecutionService(
        IWorkflowMetaRepository metaRepo,
        IWorkflowDraftRepository draftRepo,
        IWorkflowVersionRepository versionRepo,
        IWorkflowExecutionRepository executionRepo,
        IWorkflowNodeExecutionRepository nodeExecutionRepo,
        DagExecutor dagExecutor,
        IServiceScopeFactory scopeFactory,
        WorkflowExecutionCancellationRegistry cancellationRegistry,
        IIdGeneratorAccessor idGenerator,
        ILogger<WorkflowV2ExecutionService> logger)
        : this(
            metaRepo,
            draftRepo,
            versionRepo,
            executionRepo,
            nodeExecutionRepo,
            dagExecutor,
            scopeFactory,
            cancellationRegistry,
            idGenerator,
            NullAppContextAccessor.Instance,
            logger)
    {
    }

    public async Task<WorkflowV2RunResult> SyncRunAsync(
        TenantId tenantId, long workflowId, long userId, WorkflowV2RunRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("ExecutionService SyncRun start: WorkflowId={WorkflowId}", workflowId);
        var (execution, canvas, inputs) = await PrepareExecutionAsync(tenantId, workflowId, userId, request, cancellationToken);
        _logger.LogInformation(
            "ExecutionService SyncRun prepared: WorkflowId={WorkflowId} ExecutionId={ExecutionId} NodeCount={NodeCount}",
            workflowId,
            execution.Id,
            canvas.Nodes.Count);
        await _dagExecutor.RunAsync(tenantId, execution, canvas, inputs, eventChannel: null, cancellationToken);
        _logger.LogInformation(
            "ExecutionService SyncRun finished: WorkflowId={WorkflowId} ExecutionId={ExecutionId} Status={Status} Error={Error}",
            workflowId,
            execution.Id,
            execution.Status,
            execution.ErrorMessage);
        return new WorkflowV2RunResult(
            execution.Id.ToString(),
            execution.Status,
            execution.OutputsJson,
            execution.ErrorMessage);
    }

    public async Task<WorkflowV2RunResult> AsyncRunAsync(
        TenantId tenantId, long workflowId, long userId, WorkflowV2RunRequest request, CancellationToken cancellationToken)
    {
        var (execution, canvas, inputs) = await PrepareExecutionAsync(tenantId, workflowId, userId, request, cancellationToken);
        // BE-30: 工作流总超时（默认 30 分钟），防止无限制运行。
        var runCts = new CancellationTokenSource(TimeSpan.FromMinutes(30));

        if (!_cancellationRegistry.Register(execution.Id, runCts))
        {
            runCts.Dispose();
            throw new BusinessException("执行实例重复注册，无法启动异步运行。", ErrorCodes.ServerError);
        }

        // 异步执行——在后台线程中运行，不阻塞当前请求
        _ = Task.Run(async () =>
        {
            try
            {
                await RunWithScopeAsync(tenantId, execution, canvas, inputs, eventChannel: null, runCts.Token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "异步工作流执行失败: ExecutionId={ExecutionId}", execution.Id);
            }
            finally
            {
                _cancellationRegistry.Unregister(execution.Id);
            }
        }, CancellationToken.None);

        return new WorkflowV2RunResult(
            execution.Id.ToString(),
            ExecutionStatus.Pending,
            null,
            null);
    }

    public async Task CancelAsync(TenantId tenantId, long executionId, CancellationToken cancellationToken)
    {
        var execution = await _executionRepo.FindByIdAsync(tenantId, executionId, cancellationToken)
            ?? throw new BusinessException("执行实例不存在。", ErrorCodes.NotFound);

        execution.Cancel();
        await _executionRepo.UpdateAsync(execution, cancellationToken);
        _cancellationRegistry.TryCancel(executionId);
    }

    public async Task ResumeAsync(TenantId tenantId, long executionId, CancellationToken cancellationToken)
    {
        await ResumeCoreAsync(tenantId, executionId, request: null, eventChannel: null, cancellationToken);
    }

    public async Task ResumeAsync(
        TenantId tenantId,
        long executionId,
        WorkflowV2ResumeRequest? request,
        CancellationToken cancellationToken)
    {
        await ResumeCoreAsync(tenantId, executionId, request, eventChannel: null, cancellationToken);
    }

    public async IAsyncEnumerable<SseEvent> StreamResumeAsync(
        TenantId tenantId,
        long executionId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var channel = Channel.CreateUnbounded<SseEvent>(new UnboundedChannelOptions { SingleReader = true });
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var runTask = Task.Run(async () =>
        {
            try
            {
                await ResumeCoreAsync(tenantId, executionId, request: null, channel, linkedCts.Token);
            }
            catch (OperationCanceledException) when (linkedCts.IsCancellationRequested)
            {
                // noop
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "流式恢复执行失败: ExecutionId={ExecutionId}", executionId);
                await channel.Writer.WriteAsync(new SseEvent("execution_failed", JsonSerializer.Serialize(new
                {
                    executionId = executionId.ToString(),
                    errorMessage = ex.Message
                })), CancellationToken.None);
            }
            finally
            {
                channel.Writer.TryComplete();
            }
        }, CancellationToken.None);

        yield return new SseEvent("execution_resume_start", JsonSerializer.Serialize(new
        {
            executionId = executionId.ToString()
        }));

        await foreach (var evt in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return evt;
        }

        await runTask;
    }

    private async Task ResumeCoreAsync(
        TenantId tenantId,
        long executionId,
        WorkflowV2ResumeRequest? request,
        Channel<SseEvent>? eventChannel,
        CancellationToken cancellationToken)
    {
        var execution = await _executionRepo.FindByIdAsync(tenantId, executionId, cancellationToken)
            ?? throw new BusinessException("执行实例不存在。", ErrorCodes.NotFound);

        if (execution.Status != ExecutionStatus.Interrupted)
        {
            throw new BusinessException("仅中断状态的执行可恢复。", ErrorCodes.ValidationError);
        }

        var draft = await _draftRepo.FindByWorkflowIdAsync(tenantId, execution.WorkflowId, cancellationToken)
            ?? throw new BusinessException("工作流草稿不存在。", ErrorCodes.NotFound);
        var canvas = DagExecutor.ParseCanvas(draft.CanvasJson)
            ?? throw new BusinessException("画布 JSON 无效。", ErrorCodes.ValidationError);

        var nodeExecutions = await _nodeExecutionRepo.ListByExecutionIdAsync(tenantId, executionId, cancellationToken);
        var preCompletedNodeKeys = nodeExecutions
            .Where(x => x.Status == ExecutionStatus.Completed || x.Status == ExecutionStatus.Skipped)
            .Select(x => x.NodeKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var resumedInputs = MergeWithCanvasGlobals(canvas, ParseInputs(execution.InputsJson));
        foreach (var nodeExecution in nodeExecutions
                     .Where(x => x.Status == ExecutionStatus.Completed && !string.IsNullOrWhiteSpace(x.OutputsJson))
                     .OrderBy(x => x.CompletedAt ?? x.StartedAt ?? DateTime.MinValue))
        {
            var outputMap = ParseInputs(nodeExecution.OutputsJson);
            foreach (var kvp in outputMap)
            {
                resumedInputs[kvp.Key] = kvp.Value;
            }
        }

        if (!string.IsNullOrWhiteSpace(request?.InputsJson))
        {
            var resumeInputs = ParseInputs(request.InputsJson);
            foreach (var kvp in resumeInputs)
            {
                resumedInputs[kvp.Key] = kvp.Value;
            }
        }
        else if (request?.Data is { Count: > 0 })
        {
            foreach (var kvp in request.Data)
            {
                resumedInputs[kvp.Key] = kvp.Value;
            }
        }

        if (request?.VariableOverrides is { Count: > 0 })
        {
            foreach (var kvp in request.VariableOverrides)
            {
                resumedInputs[kvp.Key] = kvp.Value;
            }
        }

        execution.Resume();
        await _executionRepo.UpdateAsync(execution, cancellationToken);
        await _dagExecutor.RunAsync(
            tenantId,
            execution,
            canvas,
            resumedInputs,
            eventChannel,
            cancellationToken,
            workflowCallStack: null,
            preCompletedNodeKeys);
    }

    public async Task<WorkflowV2RunResult> DebugNodeAsync(
        TenantId tenantId, long workflowId, long userId, WorkflowV2NodeDebugRequest request, CancellationToken cancellationToken)
    {
        var meta = await _metaRepo.FindActiveByIdAsync(tenantId, workflowId, cancellationToken)
            ?? throw new BusinessException("工作流不存在。", ErrorCodes.NotFound);

        var draft = await _draftRepo.FindByWorkflowIdAsync(tenantId, meta.Id, cancellationToken)
            ?? throw new BusinessException("工作流草稿不存在。", ErrorCodes.NotFound);

        var fullCanvas = DagExecutor.ParseCanvas(draft.CanvasJson)
            ?? throw new BusinessException("画布 JSON 无效。", ErrorCodes.ValidationError);

        // 提取目标节点，构建 Entry → Target → Exit 最小子图
        var targetNode = fullCanvas.Nodes.FirstOrDefault(n =>
            string.Equals(n.Key, request.NodeKey, StringComparison.OrdinalIgnoreCase));
        if (targetNode is null)
        {
            throw new BusinessException($"节点 {request.NodeKey} 不存在。", ErrorCodes.NotFound);
        }

        var entryNode = new Domain.AiPlatform.ValueObjects.NodeSchema(
            "__debug_entry__", Domain.AiPlatform.Enums.WorkflowNodeType.Entry, "Debug Entry",
            new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase), new Domain.AiPlatform.ValueObjects.NodeLayout(0, 0, 100, 50));
        var exitNode = new Domain.AiPlatform.ValueObjects.NodeSchema(
            "__debug_exit__", Domain.AiPlatform.Enums.WorkflowNodeType.Exit, "Debug Exit",
            new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase), new Domain.AiPlatform.ValueObjects.NodeLayout(300, 0, 100, 50));

        var entryOutputPort = BuiltInWorkflowNodeDeclarations.GetDefaultOutputPortKey(WorkflowNodeType.Entry);
        var exitInputPort = BuiltInWorkflowNodeDeclarations.GetDefaultInputPortKey(WorkflowNodeType.Exit);
        var targetInputPort = BuiltInWorkflowNodeDeclarations.GetDefaultInputPortKey(targetNode.Type, fallback: string.Empty);
        var targetOutputPort = BuiltInWorkflowNodeDeclarations.GetDefaultOutputPortKey(targetNode.Type, fallback: string.Empty);
        var debugConnections = new List<Domain.AiPlatform.ValueObjects.ConnectionSchema>();
        if (!string.IsNullOrWhiteSpace(targetInputPort))
        {
            debugConnections.Add(new Domain.AiPlatform.ValueObjects.ConnectionSchema(
                "__debug_entry__",
                entryOutputPort,
                request.NodeKey,
                targetInputPort,
                null));
        }

        if (!string.IsNullOrWhiteSpace(targetOutputPort))
        {
            debugConnections.Add(new Domain.AiPlatform.ValueObjects.ConnectionSchema(
                request.NodeKey,
                targetOutputPort,
                "__debug_exit__",
                exitInputPort,
                null));
        }

        var debugCanvas = new Domain.AiPlatform.ValueObjects.CanvasSchema(
            new[] { entryNode, targetNode, exitNode },
            debugConnections);

        var inputs = MergeWithCanvasGlobals(fullCanvas, ParseInputs(request.InputsJson));
        var appId = _appContextAccessor.ResolveAppId();
        var execution = new WorkflowExecution(tenantId, workflowId, 0, userId, request.InputsJson, _idGenerator.NextId(), appId);
        await _executionRepo.AddAsync(execution, cancellationToken);

        await _dagExecutor.RunAsync(tenantId, execution, debugCanvas, inputs, eventChannel: null, cancellationToken);
        return new WorkflowV2RunResult(
            execution.Id.ToString(),
            execution.Status,
            execution.OutputsJson,
            execution.ErrorMessage,
            request.NodeKey);
    }

    public async IAsyncEnumerable<SseEvent> StreamRunAsync(
        TenantId tenantId, long workflowId, long userId, WorkflowV2RunRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var (execution, canvas, inputs) = await PrepareExecutionAsync(tenantId, workflowId, userId, request, cancellationToken);

        var channel = Channel.CreateUnbounded<SseEvent>(new UnboundedChannelOptions { SingleReader = true });
        // BE-30: 流式运行同样应用 30 分钟总超时。
        using var runCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        runCts.CancelAfter(TimeSpan.FromMinutes(30));
        if (!_cancellationRegistry.Register(execution.Id, runCts))
        {
            throw new BusinessException("执行实例重复注册，无法启动流式运行。", ErrorCodes.ServerError);
        }

        // 后台执行
        var runTask = Task.Run(async () =>
        {
            try
            {
                await RunWithScopeAsync(tenantId, execution, canvas, inputs, channel, runCts.Token);
            }
            catch (OperationCanceledException) when (runCts.IsCancellationRequested)
            {
                // 客户端断开连接导致的取消属于预期路径。
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "流式工作流执行失败: ExecutionId={ExecutionId}", execution.Id);
                channel.Writer.TryComplete();
                return;
            }
            finally
            {
                _cancellationRegistry.Unregister(execution.Id);
                channel.Writer.TryComplete();
            }
        }, CancellationToken.None);

        // 产出开始事件
        // RT-13: 附加序列号和时间戳，客户端可检测乱序/丢失事件。
        var seqCounter = new SeqCounter();
        yield return seqCounter.Enrich(new SseEvent("execution_start", JsonSerializer.Serialize(new { executionId = execution.Id.ToString() })));

        await foreach (var evt in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return seqCounter.Enrich(evt);
        }

        try
        {
            await runTask;
        }
        catch (OperationCanceledException) when (runCts.IsCancellationRequested)
        {
            // 取消路径无需额外处理。
        }

        if (cancellationToken.IsCancellationRequested)
        {
            yield break;
        }

        var latestExecution = await _executionRepo.FindByIdAsync(tenantId, execution.Id, cancellationToken);
        if (latestExecution is null)
        {
            yield return new SseEvent("execution_failed", JsonSerializer.Serialize(new
            {
                executionId = execution.Id.ToString(),
                errorMessage = "执行实例不存在。"
            }));
            yield break;
        }

        switch (latestExecution.Status)
        {
            case ExecutionStatus.Completed:
                yield return new SseEvent("execution_complete", JsonSerializer.Serialize(new
                {
                    executionId = execution.Id.ToString(),
                    outputsJson = latestExecution.OutputsJson
                }));
                break;
            case ExecutionStatus.Cancelled:
                yield return new SseEvent("execution_cancelled", JsonSerializer.Serialize(new
                {
                    executionId = execution.Id.ToString(),
                    errorMessage = latestExecution.ErrorMessage
                }));
                break;
            case ExecutionStatus.Interrupted:
                yield return new SseEvent("execution_interrupted", JsonSerializer.Serialize(new
                {
                    executionId = execution.Id.ToString(),
                    interruptType = latestExecution.InterruptType.ToString(),
                    nodeKey = latestExecution.InterruptNodeKey,
                    outputsJson = latestExecution.OutputsJson
                }));
                break;
            default:
                yield return new SseEvent("execution_failed", JsonSerializer.Serialize(new
                {
                    executionId = execution.Id.ToString(),
                    errorMessage = latestExecution.ErrorMessage ?? "工作流执行失败。"
                }));
                break;
        }
    }

    private async Task<(WorkflowExecution Execution, Domain.AiPlatform.ValueObjects.CanvasSchema Canvas, Dictionary<string, JsonElement> Inputs)>
        PrepareExecutionAsync(TenantId tenantId, long workflowId, long userId, WorkflowV2RunRequest request, CancellationToken cancellationToken)
    {
        var meta = await _metaRepo.FindActiveByIdAsync(tenantId, workflowId, cancellationToken)
            ?? throw new BusinessException("工作流不存在。", ErrorCodes.NotFound);

        var runSource = ResolveRunSource(request.Source);
        var canvasJson = await ResolveRunCanvasJsonAsync(tenantId, meta.Id, runSource, cancellationToken);
        var canvas = DagExecutor.ParseCanvas(canvasJson)
            ?? throw new BusinessException("画布 JSON 无效。", ErrorCodes.ValidationError);

        var inputs = MergeWithCanvasGlobals(canvas, ParseInputs(request.InputsJson));
        var appId = _appContextAccessor.ResolveAppId();
        var execution = new WorkflowExecution(tenantId, workflowId, meta.LatestVersionNumber, userId, request.InputsJson, _idGenerator.NextId(), appId);
        await _executionRepo.AddAsync(execution, cancellationToken);

        return (execution, canvas, inputs);
    }

    private async Task<string> ResolveRunCanvasJsonAsync(
        TenantId tenantId,
        long workflowId,
        WorkflowRunSource runSource,
        CancellationToken cancellationToken)
    {
        if (runSource == WorkflowRunSource.Published)
        {
            var latestVersion = await _versionRepo.GetLatestAsync(tenantId, workflowId, cancellationToken);
            if (latestVersion is null)
            {
                throw new BusinessException("工作流尚未发布，无法按 published 方式运行。", ErrorCodes.ValidationError);
            }

            return latestVersion.CanvasJson;
        }

        var draft = await _draftRepo.FindByWorkflowIdAsync(tenantId, workflowId, cancellationToken)
            ?? throw new BusinessException("工作流草稿不存在。", ErrorCodes.NotFound);
        return draft.CanvasJson;
    }

    private static WorkflowRunSource ResolveRunSource(string? source)
    {
        if (string.Equals(source, "draft", StringComparison.OrdinalIgnoreCase))
        {
            return WorkflowRunSource.Draft;
        }

        return WorkflowRunSource.Published;
    }

    private enum WorkflowRunSource
    {
        Published = 1,
        Draft = 2
    }

    private static Dictionary<string, JsonElement> ParseInputs(string? inputsJson)
    {
        return VariableResolver.ParseVariableDictionary(inputsJson);
    }

    private static Dictionary<string, JsonElement> MergeWithCanvasGlobals(
        Domain.AiPlatform.ValueObjects.CanvasSchema canvas,
        Dictionary<string, JsonElement> runtimeInputs)
    {
        var merged = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        if (canvas.Globals is { Count: > 0 })
        {
            foreach (var kvp in canvas.Globals)
            {
                merged[kvp.Key] = kvp.Value;
            }
        }

        foreach (var kvp in runtimeInputs)
        {
            merged[kvp.Key] = kvp.Value;
        }

        return merged;
    }

    private async Task RunWithScopeAsync(
        TenantId tenantId,
        WorkflowExecution execution,
        Domain.AiPlatform.ValueObjects.CanvasSchema canvas,
        Dictionary<string, JsonElement> inputs,
        Channel<SseEvent>? eventChannel,
        CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dagExecutor = scope.ServiceProvider.GetRequiredService<DagExecutor>();
        await dagExecutor.RunAsync(tenantId, execution, canvas, inputs, eventChannel, cancellationToken);
    }

    // RT-13: 序列号计数器辅助类，支持在 async 迭代器中使用。
    private sealed class SeqCounter
    {
        private int _seq;

        public SseEvent Enrich(SseEvent evt)
        {
            var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var currentSeq = ++_seq;
            try
            {
                using var doc = JsonDocument.Parse(evt.Data);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    return evt;
                }

                var enriched = new Dictionary<string, JsonElement>();
                foreach (var prop in root.EnumerateObject())
                {
                    enriched[prop.Name] = prop.Value;
                }

                enriched["_seq"] = JsonSerializer.SerializeToElement(currentSeq);
                enriched["_ts"] = JsonSerializer.SerializeToElement(ts);
                return new SseEvent(evt.Event, JsonSerializer.Serialize(enriched));
            }
            catch
            {
                return evt;
            }
        }
    }
}
