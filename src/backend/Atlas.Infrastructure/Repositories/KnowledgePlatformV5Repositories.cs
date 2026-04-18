using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities.Knowledge;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

/// <summary>
/// v5 §32-44 知识库专题新增的仓储集合。
/// 所有仓储继承 <see cref="RepositoryBase{TEntity}"/>，并为前端任务面板 / 检索日志面板 / 治理 Tab
/// 追加批量查询便捷方法，避免每个 service 重复写 LINQ。
/// </summary>
public sealed class KnowledgeBaseVersionRepository : RepositoryBase<KnowledgeVersionEntity>
{
    public KnowledgeBaseVersionRepository(ISqlSugarClient db) : base(db) { }

    public async Task<(List<KnowledgeVersionEntity> Items, long Total)> GetPagedAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = Db.Queryable<KnowledgeVersionEntity>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.KnowledgeBaseId == knowledgeBaseId);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(x => x.Id, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);
        return (items, total);
    }
}

public sealed class KnowledgeJobRepository : RepositoryBase<KnowledgeJob>
{
    public KnowledgeJobRepository(ISqlSugarClient db) : base(db) { }

    /// <summary>
    /// status / type 用字符串过滤（"Queued"/"Running"/... 与 "parse"/"index"/...）。
    /// KnowledgeJobService 通过 <c>StatusToString</c> / <c>TypeToString</c> 把应用层枚举转字符串后传入。
    /// </summary>
    public async Task<(List<KnowledgeJob> Items, long Total)> GetPagedAsync(
        TenantId tenantId,
        long? knowledgeBaseId,
        string? statusFilter,
        string? typeFilter,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = Db.Queryable<KnowledgeJob>()
            .Where(x => x.TenantIdValue == tenantId.Value);
        if (knowledgeBaseId.HasValue)
        {
            query = query.Where(x => x.KnowledgeBaseId == knowledgeBaseId.Value);
        }
        if (!string.IsNullOrWhiteSpace(statusFilter))
        {
            query = query.Where(x => x.Status == statusFilter);
        }
        if (!string.IsNullOrWhiteSpace(typeFilter))
        {
            query = query.Where(x => x.Type == typeFilter);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(x => x.Id, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);
        return (items, total);
    }
}

public sealed class KnowledgeBaseBindingRepository : RepositoryBase<KnowledgeBindingEntity>
{
    public KnowledgeBaseBindingRepository(ISqlSugarClient db) : base(db) { }

    public async Task<(List<KnowledgeBindingEntity> Items, long Total)> GetPagedAsync(
        TenantId tenantId,
        long? knowledgeBaseId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = Db.Queryable<KnowledgeBindingEntity>()
            .Where(x => x.TenantIdValue == tenantId.Value);
        if (knowledgeBaseId.HasValue)
        {
            query = query.Where(x => x.KnowledgeBaseId == knowledgeBaseId.Value);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(x => x.Id, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);
        return (items, total);
    }

    public async Task<int> CountByKnowledgeBaseAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        CancellationToken cancellationToken)
        => await Db.Queryable<KnowledgeBindingEntity>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.KnowledgeBaseId == knowledgeBaseId)
            .CountAsync(cancellationToken);
}

public sealed class KnowledgeBasePermissionRepository : RepositoryBase<KnowledgePermissionEntity>
{
    public KnowledgeBasePermissionRepository(ISqlSugarClient db) : base(db) { }

    public async Task<(List<KnowledgePermissionEntity> Items, long Total)> GetPagedAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = Db.Queryable<KnowledgePermissionEntity>()
            .Where(x => x.TenantIdValue == tenantId.Value
                && (x.KnowledgeBaseId == knowledgeBaseId
                    || x.KnowledgeBaseId == null /* space/project 级权限不绑定具体 KB */));

        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(x => x.Id, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);
        return (items, total);
    }
}

public sealed class KnowledgeRetrievalLogRepository : RepositoryBase<KnowledgeRetrievalLogEntity>
{
    public KnowledgeRetrievalLogRepository(ISqlSugarClient db) : base(db) { }

    public async Task<(List<KnowledgeRetrievalLogEntity> Items, long Total)> GetPagedAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        DateTime? fromTs,
        DateTime? toTs,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = Db.Queryable<KnowledgeRetrievalLogEntity>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.KnowledgeBaseId == knowledgeBaseId);
        if (fromTs.HasValue)
        {
            query = query.Where(x => x.CreatedAt >= fromTs.Value);
        }
        if (toTs.HasValue)
        {
            query = query.Where(x => x.CreatedAt <= toTs.Value);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(x => x.CreatedAt, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);
        return (items, total);
    }

    public async Task<KnowledgeRetrievalLogEntity?> FindByTraceIdAsync(
        TenantId tenantId,
        string traceId,
        CancellationToken cancellationToken)
    {
        return await Db.Queryable<KnowledgeRetrievalLogEntity>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.TraceId == traceId)
            .FirstAsync(cancellationToken);
    }
}

public sealed class KnowledgeProviderConfigRepository : RepositoryBase<KnowledgeProviderConfigEntity>
{
    public KnowledgeProviderConfigRepository(ISqlSugarClient db) : base(db) { }

    public Task<List<KnowledgeProviderConfigEntity>> ListAsync(
        TenantId tenantId,
        CancellationToken cancellationToken)
        => Db.Queryable<KnowledgeProviderConfigEntity>()
            .Where(x => x.TenantIdValue == tenantId.Value)
            .OrderBy(x => x.Role)
            .OrderBy(x => x.IsDefault, OrderByType.Desc)
            .ToListAsync(cancellationToken);

    /// <summary>v5 §39 / 计划 G1：按 role 查找默认 provider，PUT upsert 路径专用。</summary>
    public async Task<KnowledgeProviderConfigEntity?> FindDefaultByRoleAsync(
        TenantId tenantId,
        string role,
        CancellationToken cancellationToken)
    {
        return await Db.Queryable<KnowledgeProviderConfigEntity>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Role == role && x.IsDefault)
            .FirstAsync(cancellationToken);
    }

    /// <summary>清除某 role 已有的 IsDefault 标志，保证只有一个默认 provider。</summary>
    public async Task ClearDefaultByRoleAsync(
        TenantId tenantId,
        string role,
        CancellationToken cancellationToken)
    {
        var existing = await Db.Queryable<KnowledgeProviderConfigEntity>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Role == role && x.IsDefault)
            .ToListAsync(cancellationToken);
        foreach (var entity in existing)
        {
            entity.SetIsDefault(false);
            await UpdateAsync(entity, cancellationToken);
        }
    }
}

public sealed class KnowledgeTableColumnRepository : RepositoryBase<KnowledgeTableColumnEntity>
{
    public KnowledgeTableColumnRepository(ISqlSugarClient db) : base(db) { }

    public Task<List<KnowledgeTableColumnEntity>> ListByDocumentAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        long documentId,
        CancellationToken cancellationToken)
        => Db.Queryable<KnowledgeTableColumnEntity>()
            .Where(x => x.TenantIdValue == tenantId.Value
                && x.KnowledgeBaseId == knowledgeBaseId
                && x.DocumentId == documentId)
            .OrderBy(x => x.Ordinal)
            .ToListAsync(cancellationToken);
}

public sealed class KnowledgeTableRowRepository : RepositoryBase<KnowledgeTableRowEntity>
{
    public KnowledgeTableRowRepository(ISqlSugarClient db) : base(db) { }

    public async Task<(List<KnowledgeTableRowEntity> Items, long Total)> GetPagedAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        long documentId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = Db.Queryable<KnowledgeTableRowEntity>()
            .Where(x => x.TenantIdValue == tenantId.Value
                && x.KnowledgeBaseId == knowledgeBaseId
                && x.DocumentId == documentId);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(x => x.RowIndex)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);
        return (items, total);
    }
}

public sealed class KnowledgeImageItemRepository : RepositoryBase<KnowledgeImageItemEntity>
{
    public KnowledgeImageItemRepository(ISqlSugarClient db) : base(db) { }

    public async Task<(List<KnowledgeImageItemEntity> Items, long Total)> GetPagedAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        long documentId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = Db.Queryable<KnowledgeImageItemEntity>()
            .Where(x => x.TenantIdValue == tenantId.Value
                && x.KnowledgeBaseId == knowledgeBaseId
                && x.DocumentId == documentId);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(x => x.Id)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);
        return (items, total);
    }
}

public sealed class KnowledgeImageAnnotationRepository : RepositoryBase<KnowledgeImageAnnotationEntity>
{
    public KnowledgeImageAnnotationRepository(ISqlSugarClient db) : base(db) { }

    public async Task<List<KnowledgeImageAnnotationEntity>> ListByImageItemIdsAsync(
        TenantId tenantId,
        IReadOnlyList<long> imageItemIds,
        CancellationToken cancellationToken)
    {
        if (imageItemIds.Count == 0)
        {
            return new List<KnowledgeImageAnnotationEntity>();
        }

        var ids = imageItemIds.Distinct().ToArray();
        return await Db.Queryable<KnowledgeImageAnnotationEntity>()
            .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(ids, x.ImageItemId))
            .ToListAsync(cancellationToken);
    }
}

/// <summary>知识库 sidecar 元数据（kind / providerKind / chunkingProfile / retrievalProfile…）。</summary>
public sealed class KnowledgeBaseMetaRepository : RepositoryBase<KnowledgeBaseMetaEntity>
{
    public KnowledgeBaseMetaRepository(ISqlSugarClient db) : base(db) { }

    public async Task<KnowledgeBaseMetaEntity?> FindByKnowledgeBaseAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        CancellationToken cancellationToken)
    {
        return await Db.Queryable<KnowledgeBaseMetaEntity>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.KnowledgeBaseId == knowledgeBaseId)
            .FirstAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<KnowledgeBaseMetaEntity>> ListByKnowledgeBasesAsync(
        TenantId tenantId,
        IReadOnlyList<long> knowledgeBaseIds,
        CancellationToken cancellationToken)
    {
        if (knowledgeBaseIds.Count == 0)
        {
            return Array.Empty<KnowledgeBaseMetaEntity>();
        }

        var idArray = knowledgeBaseIds.Distinct().ToArray();
        return await Db.Queryable<KnowledgeBaseMetaEntity>()
            .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(idArray, x.KnowledgeBaseId))
            .ToListAsync(cancellationToken);
    }
}

/// <summary>文档 sidecar 元数据（lifecycleStatus / parsingStrategy / parseJobId / indexJobId）。</summary>
public sealed class KnowledgeDocumentMetaRepository : RepositoryBase<KnowledgeDocumentMetaEntity>
{
    public KnowledgeDocumentMetaRepository(ISqlSugarClient db) : base(db) { }

    public async Task<KnowledgeDocumentMetaEntity?> FindByDocumentAsync(
        TenantId tenantId,
        long documentId,
        CancellationToken cancellationToken)
    {
        return await Db.Queryable<KnowledgeDocumentMetaEntity>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.DocumentId == documentId)
            .FirstAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<KnowledgeDocumentMetaEntity>> ListByDocumentsAsync(
        TenantId tenantId,
        IReadOnlyList<long> documentIds,
        CancellationToken cancellationToken)
    {
        if (documentIds.Count == 0)
        {
            return Array.Empty<KnowledgeDocumentMetaEntity>();
        }
        var idArray = documentIds.Distinct().ToArray();
        return await Db.Queryable<KnowledgeDocumentMetaEntity>()
            .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(idArray, x.DocumentId))
            .ToListAsync(cancellationToken);
    }
}
