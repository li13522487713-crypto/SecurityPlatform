using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Application.AiPlatform.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.AiPlatform.Enums;
using System.Text.Json;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class WorkflowV2CommandService : IWorkflowV2CommandService
{
    private static readonly string StarterCanvasJson = JsonSerializer.Serialize(
        new
        {
            nodes = new object[]
            {
                new
                {
                    key = "entry_1",
                    type = "Entry",
                    title = "开始",
                    configs = new
                    {
                        entryVariable = "USER_INPUT",
                        entryAutoSaveHistory = true
                    },
                    inputMappings = new { },
                    layout = new
                    {
                        x = 160,
                        y = 120,
                        width = 360,
                        height = 160
                    }
                },
                new
                {
                    key = "llm_1",
                    type = "Llm",
                    title = "大模型",
                    configs = new
                    {
                        provider = "qwen",
                        model = "qwen-max",
                        prompt = "{{entry_1.output}}",
                        outputKey = "result"
                    },
                    inputMappings = new { },
                    layout = new
                    {
                        x = 620,
                        y = 120,
                        width = 360,
                        height = 160
                    }
                },
                new
                {
                    key = "exit_1",
                    type = "Exit",
                    title = "结束",
                    configs = new
                    {
                        exitTerminateMode = "return",
                        exitTemplate = "{{llm_1.result}}"
                    },
                    inputMappings = new { },
                    layout = new
                    {
                        x = 1080,
                        y = 120,
                        width = 360,
                        height = 160
                    }
                }
            },
            connections = new object[]
            {
                new
                {
                    fromNode = "entry_1",
                    fromPort = "output",
                    toNode = "llm_1",
                    toPort = "input",
                    condition = (string?)null
                },
                new
                {
                    fromNode = "llm_1",
                    fromPort = "output",
                    toNode = "exit_1",
                    toPort = "input",
                    condition = (string?)null
                }
            },
            schemaVersion = 2,
            viewport = new
            {
                x = 0,
                y = 0,
                zoom = 100
            },
            globals = new { }
        });

    private readonly IWorkflowMetaRepository _metaRepo;
    private readonly IWorkflowDraftRepository _draftRepo;
    private readonly IWorkflowVersionRepository _versionRepo;
    private readonly ICanvasValidator _canvasValidator;
    private readonly IIdGeneratorAccessor _idGenerator;

    public WorkflowV2CommandService(
        IWorkflowMetaRepository metaRepo,
        IWorkflowDraftRepository draftRepo,
        IWorkflowVersionRepository versionRepo,
        ICanvasValidator canvasValidator,
        IIdGeneratorAccessor idGenerator)
    {
        _metaRepo = metaRepo;
        _draftRepo = draftRepo;
        _versionRepo = versionRepo;
        _canvasValidator = canvasValidator;
        _idGenerator = idGenerator;
    }

    public async Task<long> CreateAsync(
        TenantId tenantId, long creatorId, WorkflowV2CreateRequest request, CancellationToken cancellationToken)
    {
        var metaId = _idGenerator.NextId();
        var meta = new WorkflowMeta(tenantId, request.Name.Trim(), request.Description?.Trim(), request.Mode, creatorId, metaId);
        await _metaRepo.AddAsync(meta, cancellationToken);

        var draft = new WorkflowDraft(tenantId, metaId, StarterCanvasJson, _idGenerator.NextId());
        await _draftRepo.AddAsync(draft, cancellationToken);

        return metaId;
    }

    public async Task SaveDraftAsync(
        TenantId tenantId, long id, WorkflowV2SaveDraftRequest request, CancellationToken cancellationToken)
    {
        var meta = await _metaRepo.FindActiveByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("工作流不存在。", ErrorCodes.NotFound);

        var draft = await _draftRepo.FindByWorkflowIdAsync(tenantId, meta.Id, cancellationToken)
            ?? throw new BusinessException("工作流草稿不存在。", ErrorCodes.NotFound);

        draft.Save(request.CanvasJson, request.CommitId);
        await _draftRepo.UpdateAsync(draft, cancellationToken);
    }

    public async Task UpdateMetaAsync(
        TenantId tenantId, long id, WorkflowV2UpdateMetaRequest request, CancellationToken cancellationToken)
    {
        var meta = await _metaRepo.FindActiveByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("工作流不存在。", ErrorCodes.NotFound);

        meta.UpdateMeta(request.Name.Trim(), request.Description?.Trim());
        await _metaRepo.UpdateAsync(meta, cancellationToken);
    }

    public async Task PublishAsync(
        TenantId tenantId, long id, long userId, WorkflowV2PublishRequest request, CancellationToken cancellationToken)
    {
        var meta = await _metaRepo.FindActiveByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("工作流不存在。", ErrorCodes.NotFound);

        var draft = await _draftRepo.FindByWorkflowIdAsync(tenantId, meta.Id, cancellationToken)
            ?? throw new BusinessException("工作流草稿不存在。", ErrorCodes.NotFound);

        var canvasValidation = _canvasValidator.ValidateCanvas(draft.CanvasJson);
        if (!canvasValidation.IsValid)
        {
            var validationMessage = string.Join("；", canvasValidation.Errors.Select(x => x.Message));
            throw new BusinessException($"工作流画布校验失败，无法发布：{validationMessage}", ErrorCodes.ValidationError);
        }

        var newVersionNumber = meta.LatestVersionNumber + 1;
        var version = new WorkflowVersion(
            tenantId, meta.Id, newVersionNumber, draft.CanvasJson, request.ChangeLog, userId, _idGenerator.NextId());
        await _versionRepo.AddAsync(version, cancellationToken);

        meta.MarkPublished(newVersionNumber);
        await _metaRepo.UpdateAsync(meta, cancellationToken);
    }

    public async Task DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        var meta = await _metaRepo.FindActiveByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("工作流不存在。", ErrorCodes.NotFound);

        meta.SoftDelete();
        await _metaRepo.UpdateAsync(meta, cancellationToken);
    }

    public async Task<long> CopyAsync(
        TenantId tenantId, long creatorId, long id, CancellationToken cancellationToken)
    {
        var meta = await _metaRepo.FindActiveByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("工作流不存在。", ErrorCodes.NotFound);

        var draft = await _draftRepo.FindByWorkflowIdAsync(tenantId, meta.Id, cancellationToken);

        var newMetaId = _idGenerator.NextId();
        var newMeta = new WorkflowMeta(tenantId, $"{meta.Name}-副本", meta.Description, meta.Mode, creatorId, newMetaId);
        await _metaRepo.AddAsync(newMeta, cancellationToken);

        var canvasJson = draft?.CanvasJson ?? StarterCanvasJson;
        var newDraft = new WorkflowDraft(tenantId, newMetaId, canvasJson, _idGenerator.NextId());
        await _draftRepo.AddAsync(newDraft, cancellationToken);

        return newMetaId;
    }

    public async Task<WorkflowVersionRollbackResult> RollbackToVersionAsync(
        TenantId tenantId, long workflowId, long versionId, long userId, CancellationToken cancellationToken)
    {
        var meta = await _metaRepo.FindActiveByIdAsync(tenantId, workflowId, cancellationToken)
            ?? throw new BusinessException("工作流不存在。", ErrorCodes.NotFound);

        var targetVersion = await _versionRepo.FindByIdAsync(tenantId, versionId, cancellationToken)
            ?? throw new BusinessException("目标版本不存在。", ErrorCodes.NotFound);

        if (targetVersion.WorkflowId != workflowId)
        {
            throw new BusinessException("目标版本不属于此工作流。", ErrorCodes.ValidationError);
        }

        var newVersionNumber = meta.LatestVersionNumber + 1;
        var rollbackChangeLog = $"回滚至版本 v{targetVersion.VersionNumber}";
        var newVersion = new WorkflowVersion(
            tenantId, workflowId, newVersionNumber, targetVersion.CanvasJson, rollbackChangeLog, userId, _idGenerator.NextId());
        await _versionRepo.AddAsync(newVersion, cancellationToken);

        var draft = await _draftRepo.FindByWorkflowIdAsync(tenantId, workflowId, cancellationToken);
        if (draft is not null)
        {
            draft.Save(targetVersion.CanvasJson, newVersion.Id.ToString());
            await _draftRepo.UpdateAsync(draft, cancellationToken);
        }

        meta.MarkPublished(newVersionNumber);
        await _metaRepo.UpdateAsync(meta, cancellationToken);

        return new WorkflowVersionRollbackResult(workflowId, versionId, newVersionNumber);
    }
}
