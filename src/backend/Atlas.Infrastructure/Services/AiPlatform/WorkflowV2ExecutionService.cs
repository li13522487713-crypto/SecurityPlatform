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
    private readonly IWorkflowExecutionRepository _executionRepo;
    private readonly DagExecutor _dagExecutor;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly WorkflowExecutionCancellationRegistry _cancellationRegistry;
    private readonly IIdGeneratorAccessor _idGenerator;
    private readonly IAppContextAccessor _appContextAccessor;
    private readonly ILogger<WorkflowV2ExecutionService> _logger;

    public WorkflowV2ExecutionService(
        IWorkflowMetaRepository metaRepo,
        IWorkflowDraftRepository draftRepo,
        IWorkflowExecutionRepository executionRepo,
        DagExecutor dagExecutor,
        IServiceScopeFactory scopeFactory,
        WorkflowExecutionCancellationRegistry cancellationRegistry,
        IIdGeneratorAccessor idGenerator,
        IAppContextAccessor appContextAccessor,
        ILogger<WorkflowV2ExecutionService> logger)
    {
        _metaRepo = metaRepo;
        _draftRepo = draftRepo;
        _executionRepo = executionRepo;
        _dagExecutor = dagExecutor;
        _scopeFactory = scopeFactory;
        _cancellationRegistry = cancellationRegistry;
        _idGenerator = idGenerator;
        _appContextAccessor = appContextAccessor;
        _logger = logger;
    }

    public WorkflowV2ExecutionService(
        IWorkflowMetaRepository metaRepo,
        IWorkflowDraftRepository draftRepo,
        IWorkflowExecutionRepository executionRepo,
        DagExecutor dagExecutor,
        IServiceScopeFactory scopeFactory,
        WorkflowExecutionCancellationRegistry cancellationRegistry,
        IIdGeneratorAccessor idGenerator,
        ILogger<WorkflowV2ExecutionService> logger)
        : this(
            metaRepo,
            draftRepo,
            executionRepo,
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
        var (execution, canvas, inputs) = await PrepareExecutionAsync(tenantId, workflowId, userId, request, cancellationToken);
        await _dagExecutor.RunAsync(tenantId, execution, canvas, inputs, eventChannel: null, cancellationToken);
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
        var runCts = new CancellationTokenSource();

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
        var execution = await _executionRepo.FindByIdAsync(tenantId, executionId, cancellationToken)
            ?? throw new BusinessException("执行实例不存在。", ErrorCodes.NotFound);

        execution.Resume();
        await _executionRepo.UpdateAsync(execution, cancellationToken);

        // 占位：TODO[coze-v2-resume] 从中断节点恢复执行
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

        var debugCanvas = new Domain.AiPlatform.ValueObjects.CanvasSchema(
            new[] { entryNode, targetNode, exitNode },
            new[]
            {
                new Domain.AiPlatform.ValueObjects.ConnectionSchema("__debug_entry__", "output", request.NodeKey, "input", null),
                new Domain.AiPlatform.ValueObjects.ConnectionSchema(request.NodeKey, "output", "__debug_exit__", "input", null)
            });

        var inputs = ParseInputs(request.InputsJson);
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
        using var runCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
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
        yield return new SseEvent("execution_start", JsonSerializer.Serialize(new { executionId = execution.Id.ToString() }));

        await foreach (var evt in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return evt;
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

        var draft = await _draftRepo.FindByWorkflowIdAsync(tenantId, meta.Id, cancellationToken)
            ?? throw new BusinessException("工作流草稿不存在。", ErrorCodes.NotFound);

        var canvas = DagExecutor.ParseCanvas(draft.CanvasJson)
            ?? throw new BusinessException("画布 JSON 无效。", ErrorCodes.ValidationError);

        var inputs = ParseInputs(request.InputsJson);
        var appId = _appContextAccessor.ResolveAppId();
        var execution = new WorkflowExecution(tenantId, workflowId, meta.LatestVersionNumber, userId, request.InputsJson, _idGenerator.NextId(), appId);
        await _executionRepo.AddAsync(execution, cancellationToken);

        return (execution, canvas, inputs);
    }

    private static Dictionary<string, JsonElement> ParseInputs(string? inputsJson)
    {
        return VariableResolver.ParseVariableDictionary(inputsJson);
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
}
