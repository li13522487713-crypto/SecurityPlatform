using System.Text.Json;
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
using Atlas.Infrastructure.Services.WorkflowEngine;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class CozeWorkflowExecutionService : ICozeWorkflowExecutionService
{
    private readonly ICozeWorkflowMetaRepository _metaRepo;
    private readonly ICozeWorkflowDraftRepository _draftRepo;
    private readonly ICozeWorkflowVersionRepository _versionRepo;
    private readonly IWorkflowExecutionRepository _executionRepo;
    private readonly IWorkflowNodeExecutionRepository _nodeExecutionRepo;
    private readonly DagExecutor _dagExecutor;
    private readonly IIdGeneratorAccessor _idGenerator;
    private readonly IAppContextAccessor _appContextAccessor;

    public CozeWorkflowExecutionService(
        ICozeWorkflowMetaRepository metaRepo,
        ICozeWorkflowDraftRepository draftRepo,
        ICozeWorkflowVersionRepository versionRepo,
        IWorkflowExecutionRepository executionRepo,
        IWorkflowNodeExecutionRepository nodeExecutionRepo,
        DagExecutor dagExecutor,
        IIdGeneratorAccessor idGenerator,
        IAppContextAccessor appContextAccessor)
    {
        _metaRepo = metaRepo;
        _draftRepo = draftRepo;
        _versionRepo = versionRepo;
        _executionRepo = executionRepo;
        _nodeExecutionRepo = nodeExecutionRepo;
        _dagExecutor = dagExecutor;
        _idGenerator = idGenerator;
        _appContextAccessor = appContextAccessor;
    }

    public async Task<CozeWorkflowRunResult> SyncRunAsync(
        TenantId tenantId,
        long workflowId,
        long userId,
        CozeWorkflowRunCommand request,
        CancellationToken cancellationToken)
    {
        var meta = await _metaRepo.FindActiveByIdAsync(tenantId, workflowId, cancellationToken)
            ?? throw new BusinessException("Coze 工作流不存在。", ErrorCodes.NotFound);

        var (schemaJson, versionNumber) = await ResolveSchemaAsync(tenantId, meta.Id, request.Source, null, cancellationToken);
        var canvas = ParseCanvasOrThrow(schemaJson);

        var inputs = VariableResolver.ParseVariableDictionary(request.InputsJson);
        var execution = new WorkflowExecution(
            tenantId,
            workflowId,
            versionNumber,
            userId,
            request.InputsJson,
            _idGenerator.NextId(),
            _appContextAccessor.ResolveAppId());
        await _executionRepo.AddAsync(execution, cancellationToken);

        await _dagExecutor.RunAsync(
            tenantId,
            execution,
            canvas,
            inputs,
            eventChannel: null,
            cancellationToken,
            userId: userId);
        return new CozeWorkflowRunResult(
            execution.Id.ToString(),
            execution.Status,
            execution.OutputsJson,
            execution.ErrorMessage);
    }

    public async Task CancelAsync(TenantId tenantId, long executionId, CancellationToken cancellationToken)
    {
        var execution = await _executionRepo.FindByIdAsync(tenantId, executionId, cancellationToken)
            ?? throw new BusinessException("执行实例不存在。", ErrorCodes.NotFound);

        execution.Cancel();
        await _executionRepo.UpdateAsync(execution, cancellationToken);
    }

    public async Task ResumeAsync(TenantId tenantId, long executionId, string? data, CancellationToken cancellationToken)
    {
        var execution = await _executionRepo.FindByIdAsync(tenantId, executionId, cancellationToken)
            ?? throw new BusinessException("执行实例不存在。", ErrorCodes.NotFound);
        if (execution.Status != ExecutionStatus.Interrupted)
        {
            throw new BusinessException("仅中断状态的执行可恢复。", ErrorCodes.ValidationError);
        }

        var draft = await _draftRepo.FindByWorkflowIdAsync(tenantId, execution.WorkflowId, cancellationToken)
            ?? throw new BusinessException("Coze 工作流草稿不存在。", ErrorCodes.NotFound);

        var canvas = ParseCanvasOrThrow(draft.SchemaJson);

        var resumedInputs = VariableResolver.ParseVariableDictionary(execution.InputsJson);
        var nodeExecutions = await _nodeExecutionRepo.ListByExecutionIdAsync(tenantId, executionId, cancellationToken);
        foreach (var nodeExecution in nodeExecutions
                     .Where(x => x.Status == ExecutionStatus.Completed && !string.IsNullOrWhiteSpace(x.OutputsJson))
                     .OrderBy(x => x.CompletedAt ?? x.StartedAt ?? DateTime.MinValue))
        {
            foreach (var (key, value) in VariableResolver.ParseVariableDictionary(nodeExecution.OutputsJson))
            {
                resumedInputs[key] = value;
            }
        }

        if (!string.IsNullOrWhiteSpace(data))
        {
            foreach (var (key, value) in VariableResolver.ParseVariableDictionary(data))
            {
                resumedInputs[key] = value;
            }
        }

        var preCompletedNodeKeys = nodeExecutions
            .Where(x => x.Status == ExecutionStatus.Completed || x.Status == ExecutionStatus.Skipped)
            .Select(x => x.NodeKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        execution.Resume();
        await _executionRepo.UpdateAsync(execution, cancellationToken);
        await _dagExecutor.RunAsync(
            tenantId,
            execution,
            canvas,
            resumedInputs,
            eventChannel: null,
            cancellationToken,
            workflowCallStack: null,
            preCompletedNodeKeys,
            userId: execution.CreatedByUserId);
    }

    public async Task<CozeWorkflowRunResult> DebugNodeAsync(
        TenantId tenantId,
        long workflowId,
        long userId,
        CozeWorkflowNodeDebugCommand request,
        CancellationToken cancellationToken)
    {
        var meta = await _metaRepo.FindActiveByIdAsync(tenantId, workflowId, cancellationToken)
            ?? throw new BusinessException("Coze 工作流不存在。", ErrorCodes.NotFound);

        var (schemaJson, versionNumber) = await ResolveSchemaAsync(tenantId, meta.Id, request.Source, request.VersionId, cancellationToken);
        var canvas = ParseCanvasOrThrow(schemaJson);

        var targetNode = canvas.Nodes.FirstOrDefault(x => string.Equals(x.Key, request.NodeKey, StringComparison.OrdinalIgnoreCase));
        if (targetNode is null)
        {
            throw new BusinessException($"节点 {request.NodeKey} 不存在。", ErrorCodes.NotFound);
        }

        var debugCanvas = new Atlas.Domain.AiPlatform.ValueObjects.CanvasSchema(
            new[] { targetNode },
            Array.Empty<Atlas.Domain.AiPlatform.ValueObjects.ConnectionSchema>());

        var inputs = VariableResolver.ParseVariableDictionary(request.InputsJson);
        var execution = new WorkflowExecution(
            tenantId,
            workflowId,
            versionNumber,
            userId,
            request.InputsJson,
            _idGenerator.NextId(),
            _appContextAccessor.ResolveAppId(),
            isDebug: true);
        await _executionRepo.AddAsync(execution, cancellationToken);

        await _dagExecutor.RunAsync(
            tenantId,
            execution,
            debugCanvas,
            inputs,
            eventChannel: null,
            cancellationToken,
            userId: userId);
        return new CozeWorkflowRunResult(
            execution.Id.ToString(),
            execution.Status,
            execution.OutputsJson,
            execution.ErrorMessage,
            request.NodeKey);
    }

    private async Task<(string SchemaJson, int VersionNumber)> ResolveSchemaAsync(
        TenantId tenantId,
        long workflowId,
        string? source,
        long? versionId,
        CancellationToken cancellationToken)
    {
        if (versionId.HasValue)
        {
            var version = await _versionRepo.FindByIdAsync(tenantId, versionId.Value, cancellationToken);
            if (version is null || version.WorkflowId != workflowId)
            {
                throw new BusinessException("指定的 Coze 工作流版本不存在。", ErrorCodes.NotFound);
            }

            return (version.SchemaJson, version.VersionNumber);
        }

        if (string.Equals(source, "draft", StringComparison.OrdinalIgnoreCase))
        {
            var draft = await _draftRepo.FindByWorkflowIdAsync(tenantId, workflowId, cancellationToken)
                ?? throw new BusinessException("Coze 工作流草稿不存在。", ErrorCodes.NotFound);
            return (draft.SchemaJson, 0);
        }

        var latestVersion = await _versionRepo.GetLatestAsync(tenantId, workflowId, cancellationToken);
        if (latestVersion is null)
        {
            throw new BusinessException("Coze 工作流尚未发布，无法按 published 方式运行。", ErrorCodes.ValidationError);
        }

        return (latestVersion.SchemaJson, latestVersion.VersionNumber);
    }

    private static Atlas.Domain.AiPlatform.ValueObjects.CanvasSchema ParseCanvasOrThrow(string schemaJson)
    {
        var canvas = DagExecutor.ParseCanvas(schemaJson);
        if (canvas is not null)
        {
            return canvas;
        }

        throw new BusinessException("Coze 工作流编译失败：画布 JSON 无法解析。", ErrorCodes.ValidationError);
    }
}
