using System.Text.Json;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Application.AiPlatform.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.AiPlatform.Enums;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class CozeWorkflowCommandService : ICozeWorkflowCommandService
{
    private static readonly string WorkflowStarterSchemaJson = JsonSerializer.Serialize(
        new
        {
            nodes = new object[]
            {
                new
                {
                    id = "entry_1",
                    type = ((int)WorkflowNodeType.Entry).ToString(),
                    meta = new
                    {
                        position = new { x = 180, y = 40 }
                    },
                    data = new
                    {
                        nodeMeta = new
                        {
                            title = "开始",
                            icon = "workflow/Entry.svg",
                            description = "工作流入口。",
                            mainColor = "#6366F1",
                            subTitle = string.Empty
                        },
                        outputs = new object[]
                        {
                            new { name = "USER_INPUT", type = "string", required = true }
                        },
                        trigger_parameters = Array.Empty<object>()
                    }
                },
                new
                {
                    id = "exit_1",
                    type = ((int)WorkflowNodeType.Exit).ToString(),
                    meta = new
                    {
                        position = new { x = 740, y = 40 }
                    },
                    data = new
                    {
                        nodeMeta = new
                        {
                            title = "结束",
                            icon = "workflow/Exit.svg",
                            description = "工作流结束并返回结果。",
                            mainColor = "#6366F1",
                            subTitle = string.Empty
                        },
                        inputs = new
                        {
                            terminatePlan = "returnVariables",
                            inputParameters = new object[]
                            {
                                new
                                {
                                    name = "USER_INPUT",
                                    input = new
                                    {
                                        value = new
                                        {
                                            content = "{{USER_INPUT}}"
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            },
            edges = new object[]
            {
                new
                {
                    sourceNodeID = "entry_1",
                    targetNodeID = "exit_1"
                }
            }
        });

    private readonly ICozeWorkflowMetaRepository _metaRepo;
    private readonly ICozeWorkflowDraftRepository _draftRepo;
    private readonly ICozeWorkflowVersionRepository _versionRepo;
    private readonly ICanvasValidator _canvasValidator;
    private readonly IIdGeneratorAccessor _idGenerator;

    public CozeWorkflowCommandService(
        ICozeWorkflowMetaRepository metaRepo,
        ICozeWorkflowDraftRepository draftRepo,
        ICozeWorkflowVersionRepository versionRepo,
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
        TenantId tenantId,
        long creatorId,
        CozeWorkflowCreateCommand request,
        CancellationToken cancellationToken)
    {
        var metaId = _idGenerator.NextId();
        var meta = new CozeWorkflowMeta(
            tenantId,
            request.Name.Trim(),
            request.Description?.Trim(),
            request.Mode,
            creatorId,
            metaId,
            request.WorkspaceId);
        await _metaRepo.AddAsync(meta, cancellationToken);

        var draft = new CozeWorkflowDraft(tenantId, metaId, WorkflowStarterSchemaJson, _idGenerator.NextId());
        await _draftRepo.AddAsync(draft, cancellationToken);
        return metaId;
    }

    public async Task SaveDraftAsync(
        TenantId tenantId,
        long id,
        CozeWorkflowSaveDraftCommand request,
        CancellationToken cancellationToken)
    {
        var meta = await _metaRepo.FindActiveByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("Coze 工作流不存在。", ErrorCodes.NotFound);

        var draft = await _draftRepo.FindByWorkflowIdAsync(tenantId, meta.Id, cancellationToken)
            ?? throw new BusinessException("Coze 工作流草稿不存在。", ErrorCodes.NotFound);

        // Optimistic-lock pre-check: request.CommitId is the OLD commit ID the client observed.
        // If the client sent a commit ID but it no longer matches the DB value, the draft
        // has been modified by another session — fail fast before touching the DB.
        var oldCommitId = request.CommitId;
        if (!string.IsNullOrWhiteSpace(oldCommitId) &&
            !string.IsNullOrWhiteSpace(draft.CommitId) &&
            !string.Equals(oldCommitId, draft.CommitId, StringComparison.Ordinal))
        {
            throw new BusinessException(
                "并发冲突：画布已被其他会话修改，请刷新后重试。",
                ErrorCodes.Conflict);
        }

        var schemaJson = string.IsNullOrWhiteSpace(request.SchemaJson) ? "{}" : request.SchemaJson;
        try
        {
            using var _ = JsonDocument.Parse(schemaJson);
        }
        catch (JsonException ex)
        {
            throw new BusinessException($"Coze 工作流 schema 不是合法 JSON：{ex.Message}", ErrorCodes.ValidationError);
        }

        var canvasValidation = _canvasValidator.ValidateCanvas(schemaJson);
        if (!canvasValidation.IsValid)
        {
            var validationMessage = string.Join("；", canvasValidation.Errors.Select(x => x.Message));
            throw new BusinessException($"Coze 工作流校验失败，无法保存：{validationMessage}", ErrorCodes.ValidationError);
        }

        // Generate a fresh commit ID so the client can detect subsequent concurrent edits.
        var newCommitId = _idGenerator.NextId().ToString();
        draft.Save(schemaJson, newCommitId);

        // Repository also enforces the commit_id constraint at the DB level.
        await _draftRepo.UpdateAsync(draft, oldCommitId, cancellationToken);
    }

    public async Task UpdateMetaAsync(
        TenantId tenantId,
        long id,
        CozeWorkflowUpdateMetaCommand request,
        CancellationToken cancellationToken)
    {
        var meta = await _metaRepo.FindActiveByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("Coze 工作流不存在。", ErrorCodes.NotFound);

        meta.UpdateMeta(request.Name.Trim(), request.Description?.Trim());
        await _metaRepo.UpdateAsync(meta, cancellationToken);
    }

    public async Task PublishAsync(
        TenantId tenantId,
        long id,
        long userId,
        CozeWorkflowPublishCommand request,
        CancellationToken cancellationToken)
    {
        var meta = await _metaRepo.FindActiveByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("Coze 工作流不存在。", ErrorCodes.NotFound);

        var draft = await _draftRepo.FindByWorkflowIdAsync(tenantId, meta.Id, cancellationToken)
            ?? throw new BusinessException("Coze 工作流草稿不存在。", ErrorCodes.NotFound);

        var canvasValidation = _canvasValidator.ValidateCanvas(draft.SchemaJson);
        if (!canvasValidation.IsValid)
        {
            var validationMessage = string.Join("；", canvasValidation.Errors.Select(x => x.Message));
            throw new BusinessException($"Coze 工作流校验失败，无法发布：{validationMessage}", ErrorCodes.ValidationError);
        }

        var newVersionNumber = meta.LatestVersionNumber + 1;
        var version = new CozeWorkflowVersion(
            tenantId,
            meta.Id,
            newVersionNumber,
            draft.SchemaJson,
            request.ChangeLog,
            userId,
            _idGenerator.NextId());
        await _versionRepo.AddAsync(version, cancellationToken);

        meta.MarkPublished(newVersionNumber);
        await _metaRepo.UpdateAsync(meta, cancellationToken);
    }

    public async Task DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        var meta = await _metaRepo.FindActiveByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("Coze 工作流不存在。", ErrorCodes.NotFound);

        meta.SoftDelete();
        await _metaRepo.UpdateAsync(meta, cancellationToken);
    }

    public async Task<long> CopyAsync(TenantId tenantId, long creatorId, long id, CancellationToken cancellationToken)
    {
        var meta = await _metaRepo.FindActiveByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("Coze 工作流不存在。", ErrorCodes.NotFound);
        var draft = await _draftRepo.FindByWorkflowIdAsync(tenantId, meta.Id, cancellationToken);

        var newMetaId = _idGenerator.NextId();
        var newMeta = new CozeWorkflowMeta(
            tenantId,
            $"{meta.Name}-副本",
            meta.Description,
            meta.Mode,
            creatorId,
            newMetaId,
            meta.WorkspaceId);
        await _metaRepo.AddAsync(newMeta, cancellationToken);

        var newDraft = new CozeWorkflowDraft(
            tenantId,
            newMetaId,
            draft?.SchemaJson ?? WorkflowStarterSchemaJson,
            _idGenerator.NextId());
        await _draftRepo.AddAsync(newDraft, cancellationToken);

        return newMetaId;
    }
}
