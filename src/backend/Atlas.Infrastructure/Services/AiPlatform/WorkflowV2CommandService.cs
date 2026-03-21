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

public sealed class WorkflowV2CommandService : IWorkflowV2CommandService
{
    private readonly IWorkflowMetaRepository _metaRepo;
    private readonly IWorkflowDraftRepository _draftRepo;
    private readonly IWorkflowVersionRepository _versionRepo;
    private readonly IIdGeneratorAccessor _idGenerator;

    public WorkflowV2CommandService(
        IWorkflowMetaRepository metaRepo,
        IWorkflowDraftRepository draftRepo,
        IWorkflowVersionRepository versionRepo,
        IIdGeneratorAccessor idGenerator)
    {
        _metaRepo = metaRepo;
        _draftRepo = draftRepo;
        _versionRepo = versionRepo;
        _idGenerator = idGenerator;
    }

    public async Task<long> CreateAsync(
        TenantId tenantId, long creatorId, WorkflowV2CreateRequest request, CancellationToken cancellationToken)
    {
        var metaId = _idGenerator.NextId();
        var meta = new WorkflowMeta(tenantId, request.Name.Trim(), request.Description?.Trim(), request.Mode, creatorId, metaId);
        await _metaRepo.AddAsync(meta, cancellationToken);

        var defaultCanvas = """{"nodes":[],"connections":[]}""";
        var draft = new WorkflowDraft(tenantId, metaId, defaultCanvas, _idGenerator.NextId());
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

        var canvasJson = draft?.CanvasJson ?? """{"nodes":[],"connections":[]}""";
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
