using System.Text.Json;
using AutoMapper;
using Atlas.Application.Audit.Abstractions;
using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Application.LowCode.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Audit.Entities;
using Atlas.Domain.LowCode.Entities;
using Microsoft.Extensions.Hosting;

namespace Atlas.Infrastructure.Services.LowCode;

/// <summary>应用定义命令服务（M01）。所有写接口均经 IAuditWriter 落审计。</summary>
public sealed class AppDefinitionCommandService : IAppDefinitionCommandService
{
    private readonly IAppDefinitionRepository _appRepo;
    private readonly IAppVersionArchiveRepository _versionRepo;
    private readonly IAuditWriter _auditWriter;
    private readonly IIdGeneratorAccessor _idGen;
    private readonly IMapper _mapper;
    private readonly IResourceReferenceIndex _referenceIndex;
    private readonly ILowCodePreviewSignal _previewSignal;
    private readonly IAppDraftLockService _draftLockService;
    private readonly IHostEnvironment _hostEnvironment;

    public AppDefinitionCommandService(
        IAppDefinitionRepository appRepo,
        IAppVersionArchiveRepository versionRepo,
        IAuditWriter auditWriter,
        IIdGeneratorAccessor idGen,
        IMapper mapper,
        IResourceReferenceIndex referenceIndex,
        ILowCodePreviewSignal previewSignal,
        IAppDraftLockService draftLockService,
        IHostEnvironment hostEnvironment)
    {
        _appRepo = appRepo;
        _versionRepo = versionRepo;
        _auditWriter = auditWriter;
        _idGen = idGen;
        _mapper = mapper;
        _referenceIndex = referenceIndex;
        _previewSignal = previewSignal;
        _draftLockService = draftLockService;
        _hostEnvironment = hostEnvironment;
    }

    public async Task<long> CreateAsync(TenantId tenantId, long currentUserId, AppDefinitionCreateRequest request, CancellationToken cancellationToken)
    {
        if (await _appRepo.ExistsCodeAsync(tenantId, request.Code, excludeId: null, cancellationToken))
        {
            throw new BusinessException(ErrorCodes.Conflict, $"应用编码已存在：{request.Code}");
        }

        var id = _idGen.NextId();
        var app = new AppDefinition(
            tenantId,
            id,
            request.Code,
            request.DisplayName,
            request.TargetTypes,
            request.DefaultLocale,
            request.WorkspaceId);
        if (!string.IsNullOrWhiteSpace(request.Description))
        {
            app.UpdateMetadata(
                request.DisplayName,
                request.Description,
                request.TargetTypes,
                request.DefaultLocale ?? "zh-CN",
                _mapper.Map<AppThemeConfig?>(request.Theme),
                currentUserId,
                request.WorkspaceId);
        }
        else if (request.Theme is not null)
        {
            app.UpdateMetadata(
                request.DisplayName,
                request.Description,
                request.TargetTypes,
                request.DefaultLocale ?? "zh-CN",
                _mapper.Map<AppThemeConfig?>(request.Theme),
                currentUserId,
                request.WorkspaceId);
        }

        app.SetCreatedByUser(currentUserId);
        await _appRepo.InsertAsync(app, cancellationToken);

        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.app.create", "success", $"app:{id}", null, null), cancellationToken);
        return id;
    }

    public async Task UpdateMetadataAsync(TenantId tenantId, long currentUserId, long id, AppDefinitionUpdateRequest request, CancellationToken cancellationToken)
    {
        var app = await _appRepo.FindByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, $"应用不存在：{id}");

        app.UpdateMetadata(
            request.DisplayName,
            request.Description,
            request.TargetTypes,
            request.DefaultLocale,
            _mapper.Map<AppThemeConfig?>(request.Theme),
            currentUserId,
            request.WorkspaceId);
        await _appRepo.UpdateAsync(app, cancellationToken);

        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.app.update", "success", $"app:{id}", null, null), cancellationToken);
    }

    public async Task DeleteAsync(TenantId tenantId, long currentUserId, long id, CancellationToken cancellationToken)
    {
        var app = await _appRepo.FindByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, $"应用不存在：{id}");

        await _appRepo.DeleteAsync(tenantId, id, cancellationToken);
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.app.delete", "success", $"app:{id}:{app.Code}", null, null), cancellationToken);
    }

    public async Task ReplaceDraftAsync(TenantId tenantId, long currentUserId, long id, AppDraftReplaceRequest request, CancellationToken cancellationToken)
    {
        EnsureValidJson(request.SchemaJson, nameof(request.SchemaJson));
        await EnsureDraftSessionCanWriteAsync(tenantId, id, request.DraftSessionId, cancellationToken);
        var app = await _appRepo.FindByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, $"应用不存在：{id}");

        app.ReplaceDraftSchema(request.SchemaJson, currentUserId);
        await _appRepo.UpdateAsync(app, cancellationToken);
        await _referenceIndex.ReindexFromSchemaJsonAsync(tenantId, id, request.SchemaJson, cancellationToken);
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.app.draft.replace", "success", $"app:{id}", null, null), cancellationToken);
        // M08 S08-3：触发 preview HMR 推送 schemaDiff（前端按收到信号重拉 draft）
        await _previewSignal.PushSchemaDiffAsync(tenantId, id.ToString(), new { kind = "replace", at = DateTimeOffset.UtcNow }, cancellationToken);
    }

    public async Task AutoSaveDraftAsync(TenantId tenantId, long currentUserId, long id, AppDraftAutoSaveRequest request, CancellationToken cancellationToken)
    {
        EnsureValidJson(request.SchemaJson, nameof(request.SchemaJson));
        await EnsureDraftSessionCanWriteAsync(tenantId, id, request.DraftSessionId, cancellationToken);
        var app = await _appRepo.FindByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, $"应用不存在：{id}");

        app.ReplaceDraftSchema(request.SchemaJson, currentUserId);
        await _appRepo.UpdateAsync(app, cancellationToken);
        await _referenceIndex.ReindexFromSchemaJsonAsync(tenantId, id, request.SchemaJson, cancellationToken);
        // autosave 只写一条简化审计（短时间内可能高频，不做完整 payload 记录）
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.app.draft.autosave", "success", $"app:{id}", null, null), cancellationToken);
        // M08 S08-3：触发 preview HMR 推送 schemaDiff
        await _previewSignal.PushSchemaDiffAsync(tenantId, id.ToString(), new { kind = "autosave", at = DateTimeOffset.UtcNow }, cancellationToken);
    }

    public async Task<long> CreateVersionSnapshotAsync(TenantId tenantId, long currentUserId, long id, AppVersionSnapshotRequest request, CancellationToken cancellationToken)
    {
        var app = await _appRepo.FindByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, $"应用不存在：{id}");

        var versionId = _idGen.NextId();
        var resourceSnapshot = string.IsNullOrWhiteSpace(request.ResourceSnapshotJson) ? "{}" : request.ResourceSnapshotJson!;
        EnsureValidJson(resourceSnapshot, "resourceSnapshotJson");

        var archive = new AppVersionArchive(
            tenantId,
            versionId,
            id,
            request.VersionLabel,
            app.DraftSchemaJson,
            resourceSnapshot,
            request.Note,
            currentUserId,
            isSystemSnapshot: false);

        await _versionRepo.InsertAsync(archive, cancellationToken);

        app.BindCurrentVersion(versionId);
        await _appRepo.UpdateAsync(app, cancellationToken);

        // 快照时刻基于最新草稿 schema 重新索引，确保引用反查表与发布版本对齐。
        await _referenceIndex.ReindexFromSchemaJsonAsync(tenantId, id, app.DraftSchemaJson, cancellationToken);
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.app.version.create", "success", $"app:{id}:version:{versionId}:label:{request.VersionLabel}", null, null), cancellationToken);
        return versionId;
    }

    private static void EnsureValidJson(string json, string fieldName)
    {
        try
        {
            using var _ = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new BusinessException(ErrorCodes.ValidationError, $"{fieldName} 不是合法 JSON：{ex.Message}");
        }
    }

    private async Task EnsureDraftSessionCanWriteAsync(TenantId tenantId, long appId, string? draftSessionId, CancellationToken cancellationToken)
    {
        var validation = await _draftLockService.ValidateAsync(tenantId, appId, draftSessionId, cancellationToken);
        if (validation.IsValid || _hostEnvironment.IsDevelopment())
        {
            return;
        }

        throw new BusinessException(ErrorCodes.Conflict, "草稿编辑锁已失效，请重新获取后再保存");
    }
}
