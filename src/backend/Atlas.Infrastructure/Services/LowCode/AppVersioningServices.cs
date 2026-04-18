using System.Text.Json;
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
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services.LowCode;

public sealed class AppVersioningService : IAppVersioningService
{
    private readonly IAppDefinitionRepository _appRepo;
    private readonly IAppVersionArchiveRepository _versionRepo;
    private readonly IIdGeneratorAccessor _idGen;
    private readonly IAuditWriter _auditWriter;
    private readonly ILogger<AppVersioningService> _logger;

    public AppVersioningService(IAppDefinitionRepository appRepo, IAppVersionArchiveRepository versionRepo, IIdGeneratorAccessor idGen, IAuditWriter auditWriter, ILogger<AppVersioningService> logger)
    {
        _appRepo = appRepo;
        _versionRepo = versionRepo;
        _idGen = idGen;
        _auditWriter = auditWriter;
        _logger = logger;
    }

    public async Task<AppVersionDiffDto> DiffAsync(TenantId tenantId, long appId, long fromVersionId, long toVersionId, CancellationToken cancellationToken)
    {
        var fromVer = await _versionRepo.FindByIdAsync(tenantId, fromVersionId, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, $"版本不存在：{fromVersionId}");
        var toVer = await _versionRepo.FindByIdAsync(tenantId, toVersionId, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, $"版本不存在：{toVersionId}");
        if (fromVer.AppId != appId || toVer.AppId != appId)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "版本与应用不匹配");
        }

        var ops = ComputeDiffOps(fromVer.SchemaSnapshotJson, toVer.SchemaSnapshotJson);
        return new AppVersionDiffDto(
            FromVersionId: fromVersionId.ToString(),
            ToVersionId: toVersionId.ToString(),
            FromLabel: fromVer.VersionLabel,
            ToLabel: toVer.VersionLabel,
            Ops: ops);
    }

    public async Task RollbackAsync(TenantId tenantId, long currentUserId, long appId, long versionId, AppVersionRollbackRequest request, CancellationToken cancellationToken)
    {
        var ver = await _versionRepo.FindByIdAsync(tenantId, versionId, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, $"版本不存在：{versionId}");
        if (ver.AppId != appId) throw new BusinessException(ErrorCodes.ValidationError, "版本与应用不匹配");

        var app = await _appRepo.FindByIdAsync(tenantId, appId, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, $"应用不存在：{appId}");
        // 把目标版本的 schema 写回草稿 + 绑定 currentVersionId
        app.ReplaceDraftSchema(ver.SchemaSnapshotJson, currentUserId);
        app.BindCurrentVersion(versionId);
        await _appRepo.UpdateAsync(app, cancellationToken);
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.app.version.rollback", "success", $"app:{appId}:to-version:{versionId}:label:{ver.VersionLabel}:note:{request.Note}", null, null), cancellationToken);
    }

    public async Task<long> ArchiveCurrentAsync(TenantId tenantId, long currentUserId, long appId, CancellationToken cancellationToken)
    {
        var app = await _appRepo.FindByIdAsync(tenantId, appId, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, $"应用不存在：{appId}");
        var versionId = _idGen.NextId();
        var label = $"runtime-archive-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
        var entity = new AppVersionArchive(tenantId, versionId, appId, label, app.DraftSchemaJson, "{}", note: "运行时归档", createdByUserId: currentUserId, isSystemSnapshot: true);
        await _versionRepo.InsertAsync(entity, cancellationToken);
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.runtime.app.version.archive", "success", $"app:{appId}:version:{versionId}", null, null), cancellationToken);
        return versionId;
    }

    /// <summary>
    /// 简化版 schema diff：把两段 JSON 反序列化后做扁平字段级比较（最多 1k 项）。
    /// 复杂图状结构 diff 由 lowcode-versioning-client 在客户端完成；此处仅给出顶层字段差异。
    /// </summary>
    private static List<AppVersionDiffOp> ComputeDiffOps(string fromJson, string toJson)
    {
        var ops = new List<AppVersionDiffOp>();
        try
        {
            using var fromDoc = JsonDocument.Parse(fromJson);
            using var toDoc = JsonDocument.Parse(toJson);
            FlattenAndDiff(string.Empty, fromDoc.RootElement, toDoc.RootElement, ops);
        }
        catch (JsonException)
        {
            ops.Add(new AppVersionDiffOp("replace", "/", fromJson.Length > 4000 ? fromJson[..4000] + "..." : fromJson, toJson.Length > 4000 ? toJson[..4000] + "..." : toJson));
        }
        return ops;
    }

    private static void FlattenAndDiff(string path, JsonElement from, JsonElement to, List<AppVersionDiffOp> ops)
    {
        if (ops.Count >= 1000) return;
        if (from.ValueKind == JsonValueKind.Object && to.ValueKind == JsonValueKind.Object)
        {
            var fromKeys = from.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
            var toKeys = to.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
            foreach (var (k, v) in fromKeys)
            {
                if (!toKeys.TryGetValue(k, out var tv))
                    ops.Add(new AppVersionDiffOp("remove", $"{path}/{k}", v.GetRawText(), null));
                else
                    FlattenAndDiff($"{path}/{k}", v, tv, ops);
            }
            foreach (var (k, v) in toKeys)
            {
                if (!fromKeys.ContainsKey(k))
                    ops.Add(new AppVersionDiffOp("add", $"{path}/{k}", null, v.GetRawText()));
            }
        }
        else if (from.GetRawText() != to.GetRawText())
        {
            ops.Add(new AppVersionDiffOp("replace", path == string.Empty ? "/" : path, from.GetRawText(), to.GetRawText()));
        }
    }
}

public sealed class ResourceReferenceGuardService : IResourceReferenceGuardService
{
    private readonly IAppResourceReferenceRepository _repo;
    private readonly IIdGeneratorAccessor _idGen;
    private readonly IAuditWriter _auditWriter;

    public ResourceReferenceGuardService(IAppResourceReferenceRepository repo, IIdGeneratorAccessor idGen, IAuditWriter auditWriter)
    {
        _repo = repo;
        _idGen = idGen;
        _auditWriter = auditWriter;
    }

    public async Task<IReadOnlyList<AppResourceReferenceDto>> ListByResourceAsync(TenantId tenantId, string resourceType, string resourceId, CancellationToken cancellationToken)
    {
        var list = await _repo.ListByResourceAsync(tenantId, resourceType, resourceId, cancellationToken);
        return list.Select(r => new AppResourceReferenceDto(
            r.Id.ToString(), r.AppId.ToString(), r.PageId?.ToString(), r.ComponentId, r.ResourceType, r.ResourceId, r.ReferencePath, r.ResourceVersion, r.CreatedAt
        )).ToList();
    }

    public async Task EnsureCanDeleteAsync(TenantId tenantId, string resourceType, string resourceId, CancellationToken cancellationToken)
    {
        var refs = await _repo.ListByResourceAsync(tenantId, resourceType, resourceId, cancellationToken);
        if (refs.Count > 0)
        {
            throw new BusinessException("RESOURCE_REFERENCED", $"资源 {resourceType}:{resourceId} 已被 {refs.Count} 处引用，无法删除");
        }
    }

    public async Task ReindexForAppAsync(TenantId tenantId, long appId, IReadOnlyList<AppResourceReferenceDto> references, CancellationToken cancellationToken)
    {
        var entities = references.Select(r => new Atlas.Domain.LowCode.Entities.AppResourceReference(
            tenantId, _idGen.NextId(), appId,
            string.IsNullOrWhiteSpace(r.PageId) ? null : long.Parse(r.PageId!),
            r.ComponentId,
            r.ResourceType, r.ResourceId, r.ReferencePath
        )).ToList();
        var inserted = await _repo.ReplaceForAppAsync(tenantId, appId, entities, cancellationToken);
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, 0L.ToString(), "lowcode.app.references.reindex", "success", $"app:{appId}:count:{inserted}", null, null), cancellationToken);
    }
}

public sealed class AppFaqService : IAppFaqService
{
    private readonly IAppFaqRepository _repo;
    private readonly IIdGeneratorAccessor _idGen;
    private readonly IAuditWriter _auditWriter;

    public AppFaqService(IAppFaqRepository repo, IIdGeneratorAccessor idGen, IAuditWriter auditWriter)
    {
        _repo = repo;
        _idGen = idGen;
        _auditWriter = auditWriter;
    }

    public async Task<IReadOnlyList<AppFaqEntryDto>> SearchAsync(TenantId tenantId, string? keyword, int pageIndex, int pageSize, CancellationToken cancellationToken)
    {
        var list = await _repo.SearchAsync(tenantId, keyword, pageIndex <= 0 ? 1 : pageIndex, pageSize <= 0 ? 20 : pageSize, cancellationToken);
        return list.Select(ToDto).ToList();
    }

    public async Task<long> UpsertAsync(TenantId tenantId, long currentUserId, AppFaqUpsertRequest request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.Id) && long.TryParse(request.Id, out var id))
        {
            var existing = await _repo.FindByIdAsync(tenantId, id, cancellationToken)
                ?? throw new BusinessException(ErrorCodes.NotFound, $"FAQ 不存在：{id}");
            existing.Update(request.Title, request.Body, request.Tags);
            await _repo.UpdateAsync(existing, cancellationToken);
            await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.faq.update", "success", $"faq:{id}", null, null), cancellationToken);
            return id;
        }
        var newId = _idGen.NextId();
        var entry = new AppFaqEntry(tenantId, newId, request.Title, request.Body, request.Tags);
        await _repo.InsertAsync(entry, cancellationToken);
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.faq.create", "success", $"faq:{newId}", null, null), cancellationToken);
        return newId;
    }

    public async Task DeleteAsync(TenantId tenantId, long currentUserId, long id, CancellationToken cancellationToken)
    {
        await _repo.DeleteAsync(tenantId, id, cancellationToken);
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.faq.delete", "success", $"faq:{id}", null, null), cancellationToken);
    }

    public async Task<AppFaqEntryDto?> HitAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        var entry = await _repo.FindByIdAsync(tenantId, id, cancellationToken);
        if (entry is null) return null;
        entry.IncrementHits();
        await _repo.UpdateAsync(entry, cancellationToken);
        return ToDto(entry);
    }

    private static AppFaqEntryDto ToDto(AppFaqEntry e) => new(e.Id.ToString(), e.Title, e.Body, e.Tags, e.Hits, e.UpdatedAt);
}
