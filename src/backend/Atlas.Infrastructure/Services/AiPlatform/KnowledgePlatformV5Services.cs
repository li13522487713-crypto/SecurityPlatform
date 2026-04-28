using System.Text.Json;
using Atlas.Application.AiPlatform.Abstractions.Knowledge;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities.Knowledge;
using Atlas.Infrastructure.Repositories;

namespace Atlas.Infrastructure.Services.AiPlatform;

/// <summary>
/// 知识库绑定关系（v5 §39）：Agent / App / Workflow / Chatflow → KB。
/// </summary>
public sealed class KnowledgeBindingService : IKnowledgeBindingService
{
    private readonly KnowledgeBaseBindingRepository _bindingRepository;
    private readonly KnowledgeBaseRepository _knowledgeBaseRepository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;

    public KnowledgeBindingService(
        KnowledgeBaseBindingRepository bindingRepository,
        KnowledgeBaseRepository knowledgeBaseRepository,
        IIdGeneratorAccessor idGeneratorAccessor)
    {
        _bindingRepository = bindingRepository;
        _knowledgeBaseRepository = knowledgeBaseRepository;
        _idGeneratorAccessor = idGeneratorAccessor;
    }

    public async Task<PagedResult<KnowledgeBindingDto>> ListAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        await EnsureKnowledgeBaseAsync(tenantId, knowledgeBaseId, cancellationToken);
        var (items, total) = await _bindingRepository.GetPagedAsync(tenantId, knowledgeBaseId, pageIndex, pageSize, cancellationToken);
        return new PagedResult<KnowledgeBindingDto>(items.Select(Map).ToList(), total, pageIndex, pageSize);
    }

    public async Task<PagedResult<KnowledgeBindingDto>> ListAllAsync(
        TenantId tenantId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var (items, total) = await _bindingRepository.GetPagedAsync(tenantId, knowledgeBaseId: null, pageIndex, pageSize, cancellationToken);
        return new PagedResult<KnowledgeBindingDto>(items.Select(Map).ToList(), total, pageIndex, pageSize);
    }

    public async Task<KnowledgeBindingDto?> GetByIdAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        long bindingId,
        CancellationToken cancellationToken)
    {
        var entity = await _bindingRepository.FindByIdAsync(tenantId, bindingId, cancellationToken);
        if (entity is null || entity.KnowledgeBaseId != knowledgeBaseId)
        {
            return null;
        }
        return Map(entity);
    }

    public async Task<long> CreateAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        KnowledgeBindingCreateRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureKnowledgeBaseAsync(tenantId, knowledgeBaseId, cancellationToken);
        var entity = new KnowledgeBindingEntity(
            tenantId,
            _idGeneratorAccessor.NextId(),
            knowledgeBaseId,
            request.CallerType.ToString().ToLowerInvariant(),
            request.CallerId,
            request.CallerName,
            request.RetrievalProfileOverride is null ? null : JsonSerializer.Serialize(request.RetrievalProfileOverride));
        await _bindingRepository.AddAsync(entity, cancellationToken);
        return entity.Id;
    }

    public async Task RemoveAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        long bindingId,
        CancellationToken cancellationToken)
    {
        var binding = await _bindingRepository.FindByIdAsync(tenantId, bindingId, cancellationToken)
            ?? throw new BusinessException("绑定关系不存在。", ErrorCodes.NotFound);
        if (binding.KnowledgeBaseId != knowledgeBaseId)
        {
            throw new BusinessException("绑定不属于该知识库。", ErrorCodes.Forbidden);
        }

        await _bindingRepository.DeleteAsync(tenantId, bindingId, cancellationToken);
    }

    private async Task EnsureKnowledgeBaseAsync(TenantId tenantId, long knowledgeBaseId, CancellationToken cancellationToken)
    {
        var kb = await _knowledgeBaseRepository.FindByIdAsync(tenantId, knowledgeBaseId, cancellationToken);
        if (kb is null)
        {
            throw new BusinessException("知识库不存在。", ErrorCodes.NotFound);
        }
    }

    private static KnowledgeBindingDto Map(KnowledgeBindingEntity entity)
        => new(
            entity.Id,
            entity.KnowledgeBaseId,
            ParseCallerType(entity.CallerType),
            entity.CallerId,
            entity.CallerName,
            entity.CreatedAt,
            entity.UpdatedAt,
            entity.RetrievalProfileOverrideJson);

    private static KnowledgeBindingCallerType ParseCallerType(string value)
        => value?.ToLowerInvariant() switch
        {
            "app" => KnowledgeBindingCallerType.App,
            "workflow" => KnowledgeBindingCallerType.Workflow,
            "chatflow" => KnowledgeBindingCallerType.Chatflow,
            _ => KnowledgeBindingCallerType.Agent
        };
}

/// <summary>知识库四层权限（v5 §39）。</summary>
public sealed class KnowledgePermissionService : IKnowledgePermissionService
{
    private readonly KnowledgeBasePermissionRepository _permissionRepository;
    private readonly KnowledgeBaseRepository _knowledgeBaseRepository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;

    public KnowledgePermissionService(
        KnowledgeBasePermissionRepository permissionRepository,
        KnowledgeBaseRepository knowledgeBaseRepository,
        IIdGeneratorAccessor idGeneratorAccessor)
    {
        _permissionRepository = permissionRepository;
        _knowledgeBaseRepository = knowledgeBaseRepository;
        _idGeneratorAccessor = idGeneratorAccessor;
    }

    public async Task<PagedResult<KnowledgePermissionDto>> ListAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        await EnsureKnowledgeBaseAsync(tenantId, knowledgeBaseId, cancellationToken);
        var (items, total) = await _permissionRepository.GetPagedAsync(tenantId, knowledgeBaseId, pageIndex, pageSize, cancellationToken);
        return new PagedResult<KnowledgePermissionDto>(items.Select(Map).ToList(), total, pageIndex, pageSize);
    }

    public async Task<long> GrantAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        KnowledgePermissionGrantRequest request,
        string grantedByUserId,
        CancellationToken cancellationToken)
    {
        await EnsureKnowledgeBaseAsync(tenantId, knowledgeBaseId, cancellationToken);
        var entity = new KnowledgePermissionEntity(
            tenantId,
            _idGeneratorAccessor.NextId(),
            request.Scope.ToString().ToLowerInvariant(),
            request.ScopeId,
            request.KnowledgeBaseId ?? knowledgeBaseId,
            request.DocumentId,
            request.SubjectType.ToString().ToLowerInvariant(),
            request.SubjectId,
            request.SubjectName,
            JsonSerializer.Serialize(request.Actions),
            grantedByUserId);
        await _permissionRepository.AddAsync(entity, cancellationToken);
        return entity.Id;
    }

    public async Task UpdateAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        long permissionId,
        KnowledgePermissionUpdateRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureKnowledgeBaseAsync(tenantId, knowledgeBaseId, cancellationToken);
        var entity = await _permissionRepository.FindByIdAsync(tenantId, permissionId, cancellationToken)
            ?? throw new BusinessException("权限记录不存在。", ErrorCodes.NotFound);
        if (entity.KnowledgeBaseId.HasValue && entity.KnowledgeBaseId.Value != knowledgeBaseId)
        {
            throw new BusinessException("权限记录不属于该知识库。", ErrorCodes.Forbidden);
        }

        entity.UpdateActions(JsonSerializer.Serialize(request.Actions));
        await _permissionRepository.UpdateAsync(entity, cancellationToken);
    }

    public async Task RevokeAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        long permissionId,
        CancellationToken cancellationToken)
    {
        var entity = await _permissionRepository.FindByIdAsync(tenantId, permissionId, cancellationToken)
            ?? throw new BusinessException("权限记录不存在。", ErrorCodes.NotFound);
        await _permissionRepository.DeleteAsync(tenantId, permissionId, cancellationToken);
        _ = entity;
    }

    private async Task EnsureKnowledgeBaseAsync(TenantId tenantId, long knowledgeBaseId, CancellationToken cancellationToken)
    {
        var kb = await _knowledgeBaseRepository.FindByIdAsync(tenantId, knowledgeBaseId, cancellationToken);
        if (kb is null)
        {
            throw new BusinessException("知识库不存在。", ErrorCodes.NotFound);
        }
    }

    private static KnowledgePermissionDto Map(KnowledgePermissionEntity entity)
    {
        IReadOnlyList<KnowledgePermissionAction> actions = TryParseActions(entity.ActionsJson);
        return new KnowledgePermissionDto(
            entity.Id,
            ParseScope(entity.Scope),
            entity.ScopeId,
            ParseSubjectType(entity.SubjectType),
            entity.SubjectId,
            entity.SubjectName,
            actions,
            entity.GrantedBy,
            entity.GrantedAt,
            entity.KnowledgeBaseId,
            entity.DocumentId);
    }

    private static IReadOnlyList<KnowledgePermissionAction> TryParseActions(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<KnowledgePermissionAction>>(json) ?? new();
        }
        catch (JsonException)
        {
            return Array.Empty<KnowledgePermissionAction>();
        }
    }

    private static KnowledgePermissionScope ParseScope(string value)
        => value?.ToLowerInvariant() switch
        {
            "space" => KnowledgePermissionScope.Space,
            "project" => KnowledgePermissionScope.Project,
            "document" => KnowledgePermissionScope.Document,
            _ => KnowledgePermissionScope.KnowledgeBase
        };

    private static KnowledgePermissionSubjectType ParseSubjectType(string value)
        => value?.ToLowerInvariant() switch
        {
            "role" => KnowledgePermissionSubjectType.Role,
            "group" => KnowledgePermissionSubjectType.Group,
            _ => KnowledgePermissionSubjectType.User
        };
}

/// <summary>知识库版本治理（v5 §40）。</summary>
public sealed class KnowledgeVersionService : IKnowledgeVersionService
{
    private readonly KnowledgeBaseVersionRepository _versionRepository;
    private readonly KnowledgeBaseRepository _knowledgeBaseRepository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;

    public KnowledgeVersionService(
        KnowledgeBaseVersionRepository versionRepository,
        KnowledgeBaseRepository knowledgeBaseRepository,
        IIdGeneratorAccessor idGeneratorAccessor)
    {
        _versionRepository = versionRepository;
        _knowledgeBaseRepository = knowledgeBaseRepository;
        _idGeneratorAccessor = idGeneratorAccessor;
    }

    public async Task<PagedResult<KnowledgeVersionDto>> ListAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        await EnsureKnowledgeBaseAsync(tenantId, knowledgeBaseId, cancellationToken);
        var (items, total) = await _versionRepository.GetPagedAsync(tenantId, knowledgeBaseId, pageIndex, pageSize, cancellationToken);
        return new PagedResult<KnowledgeVersionDto>(items.Select(Map).ToList(), total, pageIndex, pageSize);
    }

    public async Task<long> CreateSnapshotAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        KnowledgeVersionCreateRequest request,
        string createdByUserId,
        CancellationToken cancellationToken)
    {
        var kb = await _knowledgeBaseRepository.FindByIdAsync(tenantId, knowledgeBaseId, cancellationToken)
            ?? throw new BusinessException("知识库不存在。", ErrorCodes.NotFound);
        var id = _idGeneratorAccessor.NextId();
        var snapshotRef = $"snapshot-{knowledgeBaseId}-{id}";
        var entity = new KnowledgeDocumentVersion(
            tenantId,
            id,
            knowledgeBaseId,
            request.Label,
            request.Note,
            snapshotRef,
            kb.DocumentCount,
            kb.ChunkCount,
            createdByUserId,
            "draft");
        await _versionRepository.AddAsync(entity, cancellationToken);
        return entity.Id;
    }

    public async Task ReleaseAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        long versionId,
        CancellationToken cancellationToken)
    {
        var entity = await _versionRepository.FindByIdAsync(tenantId, versionId, cancellationToken)
            ?? throw new BusinessException("版本不存在。", ErrorCodes.NotFound);
        if (entity.KnowledgeBaseId != knowledgeBaseId)
        {
            throw new BusinessException("版本不属于该知识库。", ErrorCodes.Forbidden);
        }
        entity.Release(DateTime.UtcNow);
        await _versionRepository.UpdateAsync(entity, cancellationToken);
    }

    public async Task RollbackAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        long versionId,
        CancellationToken cancellationToken)
    {
        var target = await _versionRepository.FindByIdAsync(tenantId, versionId, cancellationToken)
            ?? throw new BusinessException("版本不存在。", ErrorCodes.NotFound);
        if (target.KnowledgeBaseId != knowledgeBaseId)
        {
            throw new BusinessException("版本不属于该知识库。", ErrorCodes.Forbidden);
        }

        // v5 §40 / 计划 G3：rollback 实现：
        // 1) 用一条新的 "released" 版本记录指向被回退到的 snapshotRef，让 listVersions 能看到 trace
        // 2) 把 KB 当前 Document/Chunk 计数重置为目标版本快照的计数（不动文件存储，只改元信息）
        var kb = await _knowledgeBaseRepository.FindByIdAsync(tenantId, knowledgeBaseId, cancellationToken)
            ?? throw new BusinessException("知识库不存在。", ErrorCodes.NotFound);

        var newId = _idGeneratorAccessor.NextId();
        var rollbackEntry = new KnowledgeDocumentVersion(
            tenantId,
            newId,
            knowledgeBaseId,
            label: $"rollback-to-{target.Label}",
            note: $"Rolled back to version {target.Label} ({target.SnapshotRef})",
            snapshotRef: target.SnapshotRef,
            documentCount: target.DocumentCount,
            chunkCount: target.ChunkCount,
            createdBy: "system",
            status: "released");
        rollbackEntry.MarkRolledBack(DateTime.UtcNow, target.SnapshotRef);
        await _versionRepository.AddAsync(rollbackEntry, cancellationToken);

        // 反向写入 KB 计数；保留实际数据不变（v5 §40 仅 Schema 恢复，不改物理 chunks）
        kb.SetDocumentCount(target.DocumentCount);
        kb.SetChunkCount(target.ChunkCount);
        await _knowledgeBaseRepository.UpdateAsync(kb, cancellationToken);
    }

    public async Task<KnowledgeVersionDiffDto> DiffAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        long fromVersionId,
        long toVersionId,
        CancellationToken cancellationToken)
    {
        var from = await _versionRepository.FindByIdAsync(tenantId, fromVersionId, cancellationToken)
            ?? throw new BusinessException("源版本不存在。", ErrorCodes.NotFound);
        var to = await _versionRepository.FindByIdAsync(tenantId, toVersionId, cancellationToken)
            ?? throw new BusinessException("目标版本不存在。", ErrorCodes.NotFound);
        if (from.KnowledgeBaseId != knowledgeBaseId || to.KnowledgeBaseId != knowledgeBaseId)
        {
            throw new BusinessException("版本不属于该知识库。", ErrorCodes.Forbidden);
        }

        var docDelta = to.DocumentCount - from.DocumentCount;
        var chunkDelta = to.ChunkCount - from.ChunkCount;
        var entries = new List<KnowledgeVersionDiffEntry>
        {
            new("document", docDelta >= 0 ? "added" : "removed", $"{from.Label} → {to.Label}", $"documents {(docDelta >= 0 ? "+" : "")}{docDelta}"),
            new("chunk", chunkDelta >= 0 ? "added" : "removed", $"{from.Label} → {to.Label}", $"chunks {(chunkDelta >= 0 ? "+" : "")}{chunkDelta}")
        };
        return new KnowledgeVersionDiffDto(fromVersionId, toVersionId, entries);
    }

    private async Task EnsureKnowledgeBaseAsync(TenantId tenantId, long knowledgeBaseId, CancellationToken cancellationToken)
    {
        var kb = await _knowledgeBaseRepository.FindByIdAsync(tenantId, knowledgeBaseId, cancellationToken);
        if (kb is null)
        {
            throw new BusinessException("知识库不存在。", ErrorCodes.NotFound);
        }
    }

    private static KnowledgeVersionDto Map(KnowledgeDocumentVersion entity)
        => new(
            entity.Id,
            entity.KnowledgeBaseId,
            entity.Label,
            ParseStatus(entity.Status),
            entity.SnapshotRef,
            entity.DocumentCount,
            entity.ChunkCount,
            entity.CreatedBy,
            entity.CreatedAt,
            entity.Note,
            entity.ReleasedAt);

    private static KnowledgeVersionStatus ParseStatus(string value)
        => value?.ToLowerInvariant() switch
        {
            "released" => KnowledgeVersionStatus.Released,
            "archived" => KnowledgeVersionStatus.Archived,
            _ => KnowledgeVersionStatus.Draft
        };
}

/// <summary>Provider 配置中心（v5 §39 / §42 / 计划 G1+G5）。
/// 列表只读 + admin upsert（按 role 维护默认 provider）。</summary>
public sealed class KnowledgeProviderConfigService : IKnowledgeProviderConfigService
{
    private readonly KnowledgeProviderConfigRepository _repository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;

    public KnowledgeProviderConfigService(
        KnowledgeProviderConfigRepository repository,
        IIdGeneratorAccessor idGeneratorAccessor)
    {
        _repository = repository;
        _idGeneratorAccessor = idGeneratorAccessor;
    }

    public async Task<IReadOnlyList<KnowledgeProviderConfigDto>> ListAsync(
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var entities = await _repository.ListAsync(tenantId, cancellationToken);
        return entities.Select(Map).ToList();
    }

    public async Task<KnowledgeProviderConfigDto> UpsertAsync(
        TenantId tenantId,
        KnowledgeProviderConfigUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var roleString = request.Role.ToString().ToLowerInvariant();
        var statusString = request.Status.ToString().ToLowerInvariant();

        // 若标记为默认，先清除该 role 当前默认标志，确保只有一个默认 provider
        if (request.IsDefault)
        {
            await _repository.ClearDefaultByRoleAsync(tenantId, roleString, cancellationToken);
        }

        var existing = await _repository.FindDefaultByRoleAsync(tenantId, roleString, cancellationToken);
        if (existing is not null && request.IsDefault)
        {
            existing.Upsert(
                request.ProviderName,
                request.DisplayName,
                statusString,
                request.IsDefault,
                request.Endpoint,
                request.Region,
                request.BucketOrIndex,
                request.MetadataJson);
            await _repository.UpdateAsync(existing, cancellationToken);
            return Map(existing);
        }

        var configId = $"{roleString}-{request.ProviderName.ToLowerInvariant()}";
        var entity = new KnowledgeProviderConfigEntity(
            tenantId,
            _idGeneratorAccessor.NextId(),
            configId,
            roleString,
            request.ProviderName,
            request.DisplayName,
            request.IsDefault,
            statusString,
            request.Endpoint,
            request.Region,
            request.BucketOrIndex,
            request.MetadataJson);
        await _repository.AddAsync(entity, cancellationToken);
        return Map(entity);
    }

    private static KnowledgeProviderConfigDto Map(KnowledgeProviderConfigEntity entity)
        => new(
            entity.ConfigId,
            ParseRole(entity.Role),
            entity.ProviderName,
            entity.DisplayName,
            ParseStatus(entity.Status),
            entity.IsDefault,
            entity.UpdatedAt,
            entity.Endpoint,
            entity.Region,
            entity.BucketOrIndex,
            entity.MetadataJson);

    private static KnowledgeProviderRole ParseRole(string value)
        => value?.ToLowerInvariant() switch
        {
            "storage" => KnowledgeProviderRole.Storage,
            "vector" => KnowledgeProviderRole.Vector,
            "embedding" => KnowledgeProviderRole.Embedding,
            "generation" => KnowledgeProviderRole.Generation,
            _ => KnowledgeProviderRole.Upload
        };

    private static KnowledgeProviderStatus ParseStatus(string value)
        => value?.ToLowerInvariant() switch
        {
            "degraded" => KnowledgeProviderStatus.Degraded,
            "inactive" => KnowledgeProviderStatus.Inactive,
            _ => KnowledgeProviderStatus.Active
        };
}

/// <summary>表格知识库列/行视图（v5 §37）。</summary>
public sealed class KnowledgeTableViewService : IKnowledgeTableViewService
{
    private readonly KnowledgeTableColumnRepository _columnRepository;
    private readonly KnowledgeTableRowRepository _rowRepository;

    public KnowledgeTableViewService(
        KnowledgeTableColumnRepository columnRepository,
        KnowledgeTableRowRepository rowRepository)
    {
        _columnRepository = columnRepository;
        _rowRepository = rowRepository;
    }

    public async Task<IReadOnlyList<KnowledgeTableColumnDto>> ListColumnsAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        long documentId,
        CancellationToken cancellationToken)
    {
        var entities = await _columnRepository.ListByDocumentAsync(tenantId, knowledgeBaseId, documentId, cancellationToken);
        return entities.Select(Map).ToList();
    }

    public async Task<PagedResult<KnowledgeTableRowDto>> ListRowsAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        long documentId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var (items, total) = await _rowRepository.GetPagedAsync(tenantId, knowledgeBaseId, documentId, pageIndex, pageSize, cancellationToken);
        return new PagedResult<KnowledgeTableRowDto>(
            items.Select(Map).ToList(),
            total,
            pageIndex,
            pageSize);
    }

    private static KnowledgeTableColumnDto Map(KnowledgeTableColumnEntity entity)
        => new(
            entity.Id,
            entity.KnowledgeBaseId,
            entity.DocumentId,
            entity.Ordinal,
            entity.Name,
            entity.IsIndexColumn,
            ParseDataType(entity.DataType));

    private static KnowledgeTableRowDto Map(KnowledgeTableRowEntity entity)
        => new(
            entity.Id,
            entity.KnowledgeBaseId,
            entity.DocumentId,
            entity.RowIndex,
            entity.CellsJson,
            entity.ChunkId);

    private static KnowledgeTableColumnDataType ParseDataType(string value)
        => value?.ToLowerInvariant() switch
        {
            "number" => KnowledgeTableColumnDataType.Number,
            "boolean" => KnowledgeTableColumnDataType.Boolean,
            "date" => KnowledgeTableColumnDataType.Date,
            _ => KnowledgeTableColumnDataType.String
        };
}

/// <summary>图片知识库的项目与标注视图（v5 §37）。</summary>
public sealed class KnowledgeImageItemService : IKnowledgeImageItemService
{
    private readonly KnowledgeImageItemRepository _itemRepository;
    private readonly KnowledgeImageAnnotationRepository _annotationRepository;

    public KnowledgeImageItemService(
        KnowledgeImageItemRepository itemRepository,
        KnowledgeImageAnnotationRepository annotationRepository)
    {
        _itemRepository = itemRepository;
        _annotationRepository = annotationRepository;
    }

    public async Task<PagedResult<KnowledgeImageItemDto>> ListAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        long documentId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var (items, total) = await _itemRepository.GetPagedAsync(tenantId, knowledgeBaseId, documentId, pageIndex, pageSize, cancellationToken);
        var ids = items.Select(item => item.Id).ToList();
        var annotations = await _annotationRepository.ListByImageItemIdsAsync(tenantId, ids, cancellationToken);
        var grouped = annotations.GroupBy(a => a.ImageItemId).ToDictionary(g => g.Key, g => g.ToList());

        var dtos = items.Select(item => new KnowledgeImageItemDto(
            item.Id,
            item.KnowledgeBaseId,
            item.DocumentId,
            item.FileName,
            grouped.TryGetValue(item.Id, out var annList)
                ? annList.Select(MapAnnotation).ToList()
                : new List<KnowledgeImageAnnotationDto>(),
            item.FileId,
            item.Width,
            item.Height,
            item.ThumbnailUrl)).ToList();

        return new PagedResult<KnowledgeImageItemDto>(dtos, total, pageIndex, pageSize);
    }

    private static KnowledgeImageAnnotationDto MapAnnotation(KnowledgeImageAnnotationEntity entity)
        => new(
            entity.Id,
            entity.ImageItemId,
            ParseAnnotationType(entity.Type),
            entity.Text,
            entity.Confidence);

    private static KnowledgeImageAnnotationType ParseAnnotationType(string value)
        => value?.ToLowerInvariant() switch
        {
            "ocr" => KnowledgeImageAnnotationType.Ocr,
            "tag" => KnowledgeImageAnnotationType.Tag,
            "vlm" => KnowledgeImageAnnotationType.Vlm,
            _ => KnowledgeImageAnnotationType.Caption
        };
}
