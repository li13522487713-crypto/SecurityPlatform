using Atlas.Application.Microflows.Repositories;
using Atlas.Domain.Microflows.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories.Microflows;

public sealed class MicroflowSchemaSnapshotRepository : IMicroflowSchemaSnapshotRepository
{
    private readonly ISqlSugarClient _db;

    public MicroflowSchemaSnapshotRepository(ISqlSugarClient db) => _db = db;

    public Task<MicroflowSchemaSnapshotEntity?> GetByIdAsync(string id, CancellationToken cancellationToken)
        => _db.Queryable<MicroflowSchemaSnapshotEntity>().Where(x => x.Id == id).FirstAsync(cancellationToken)!;

    public async Task<IReadOnlyList<MicroflowSchemaSnapshotEntity>> ListByResourceIdAsync(string resourceId, CancellationToken cancellationToken)
        => await _db.Queryable<MicroflowSchemaSnapshotEntity>()
            .Where(x => x.ResourceId == resourceId)
            .OrderBy(x => x.CreatedAt, OrderByType.Desc)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<MicroflowSchemaSnapshotEntity>> ListByIdsAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        var snapshotIds = ids.Where(static id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToArray();
        if (snapshotIds.Length == 0)
        {
            return Array.Empty<MicroflowSchemaSnapshotEntity>();
        }

        return await _db.Queryable<MicroflowSchemaSnapshotEntity>()
            .Where(x => SqlFunc.ContainsArray(snapshotIds, x.Id))
            .ToListAsync(cancellationToken);
    }

    public Task InsertAsync(MicroflowSchemaSnapshotEntity entity, CancellationToken cancellationToken)
        => _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);

    public Task<MicroflowSchemaSnapshotEntity?> GetLatestByResourceIdAsync(string resourceId, CancellationToken cancellationToken)
        => _db.Queryable<MicroflowSchemaSnapshotEntity>()
            .Where(x => x.ResourceId == resourceId)
            .OrderBy(x => x.CreatedAt, OrderByType.Desc)
            .FirstAsync(cancellationToken)!;
}

public sealed class MicroflowVersionRepository : IMicroflowVersionRepository
{
    private readonly ISqlSugarClient _db;

    public MicroflowVersionRepository(ISqlSugarClient db) => _db = db;

    public async Task<IReadOnlyList<MicroflowVersionEntity>> ListByResourceIdAsync(string resourceId, CancellationToken cancellationToken)
        => await _db.Queryable<MicroflowVersionEntity>()
            .Where(x => x.ResourceId == resourceId)
            .OrderBy(x => x.CreatedAt, OrderByType.Desc)
            .ToListAsync(cancellationToken);

    public Task<MicroflowVersionEntity?> GetByIdAsync(string id, CancellationToken cancellationToken)
        => _db.Queryable<MicroflowVersionEntity>().Where(x => x.Id == id).FirstAsync(cancellationToken)!;

    public Task<MicroflowVersionEntity?> GetByResourceVersionAsync(string resourceId, string version, CancellationToken cancellationToken)
        => _db.Queryable<MicroflowVersionEntity>()
            .Where(x => x.ResourceId == resourceId && x.Version == version)
            .FirstAsync(cancellationToken)!;

    public Task InsertAsync(MicroflowVersionEntity entity, CancellationToken cancellationToken)
        => _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);

    public async Task MarkLatestPublishedAsync(string resourceId, string versionId, CancellationToken cancellationToken)
    {
        await _db.Updateable<MicroflowVersionEntity>()
            .SetColumns(x => x.IsLatestPublished == false)
            .Where(x => x.ResourceId == resourceId)
            .ExecuteCommandAsync(cancellationToken);

        await _db.Updateable<MicroflowVersionEntity>()
            .SetColumns(x => x.IsLatestPublished == true)
            .Where(x => x.Id == versionId && x.ResourceId == resourceId)
            .ExecuteCommandAsync(cancellationToken);
    }
}

public sealed class MicroflowPublishSnapshotRepository : IMicroflowPublishSnapshotRepository
{
    private readonly ISqlSugarClient _db;

    public MicroflowPublishSnapshotRepository(ISqlSugarClient db) => _db = db;

    public Task<MicroflowPublishSnapshotEntity?> GetByIdAsync(string id, CancellationToken cancellationToken)
        => _db.Queryable<MicroflowPublishSnapshotEntity>().Where(x => x.Id == id).FirstAsync(cancellationToken)!;

    public Task<MicroflowPublishSnapshotEntity?> GetLatestByResourceIdAsync(string resourceId, CancellationToken cancellationToken)
        => _db.Queryable<MicroflowPublishSnapshotEntity>()
            .Where(x => x.ResourceId == resourceId)
            .OrderBy(x => x.PublishedAt, OrderByType.Desc)
            .FirstAsync(cancellationToken)!;

    public Task<MicroflowPublishSnapshotEntity?> GetByResourceVersionAsync(string resourceId, string version, CancellationToken cancellationToken)
        => _db.Queryable<MicroflowPublishSnapshotEntity>()
            .Where(x => x.ResourceId == resourceId && x.Version == version)
            .OrderBy(x => x.PublishedAt, OrderByType.Desc)
            .FirstAsync(cancellationToken)!;

    public Task InsertAsync(MicroflowPublishSnapshotEntity entity, CancellationToken cancellationToken)
        => _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
}

public sealed class MicroflowReferenceRepository : IMicroflowReferenceRepository
{
    private readonly ISqlSugarClient _db;

    public MicroflowReferenceRepository(ISqlSugarClient db) => _db = db;

    public async Task<IReadOnlyList<MicroflowReferenceEntity>> ListByTargetMicroflowIdAsync(
        string targetMicroflowId,
        bool includeInactive,
        CancellationToken cancellationToken)
        => await ListByTargetMicroflowIdAsync(
            targetMicroflowId,
            new MicroflowReferenceQuery { IncludeInactive = includeInactive },
            cancellationToken);

    public async Task<IReadOnlyList<MicroflowReferenceEntity>> ListByTargetMicroflowIdAsync(
        string targetMicroflowId,
        MicroflowReferenceQuery query,
        CancellationToken cancellationToken)
    {
        var q = _db.Queryable<MicroflowReferenceEntity>().Where(x => x.TargetMicroflowId == targetMicroflowId);
        q = ApplyReferenceFilters(q, query);
        return await q.OrderBy(x => x.UpdatedAt, OrderByType.Desc).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MicroflowReferenceEntity>> ListBySourceAsync(
        string sourceType,
        string sourceId,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<MicroflowReferenceEntity>()
            .Where(x => x.SourceType == sourceType && x.SourceId == sourceId)
            .OrderBy(x => x.UpdatedAt, OrderByType.Desc)
            .ToListAsync(cancellationToken);
    }

    public async Task UpsertReferencesAsync(
        string targetMicroflowId,
        IReadOnlyList<MicroflowReferenceEntity> references,
        CancellationToken cancellationToken)
    {
        await DeleteByTargetMicroflowIdAsync(targetMicroflowId, cancellationToken);
        await InsertManyAsync(references, cancellationToken);
    }

    public async Task UpsertReferencesForSourceAsync(
        string sourceType,
        string sourceId,
        IReadOnlyList<MicroflowReferenceEntity> references,
        CancellationToken cancellationToken)
    {
        await DeleteBySourceAsync(sourceType, sourceId, cancellationToken);
        await InsertManyAsync(references, cancellationToken);
    }

    public Task InsertManyAsync(IReadOnlyList<MicroflowReferenceEntity> references, CancellationToken cancellationToken)
        => references.Count == 0
            ? Task.CompletedTask
            : _db.Insertable(references.ToList()).ExecuteCommandAsync(cancellationToken);

    public Task<int> CountByTargetMicroflowIdAsync(
        string targetMicroflowId,
        MicroflowReferenceQuery query,
        CancellationToken cancellationToken)
    {
        var q = _db.Queryable<MicroflowReferenceEntity>().Where(x => x.TargetMicroflowId == targetMicroflowId);
        return ApplyReferenceFilters(q, query).CountAsync(cancellationToken);
    }

    public Task DeleteBySourceAsync(string sourceType, string sourceId, CancellationToken cancellationToken)
        => _db.Deleteable<MicroflowReferenceEntity>()
            .Where(x => x.SourceType == sourceType && x.SourceId == sourceId)
            .ExecuteCommandAsync(cancellationToken);

    public Task DeleteByTargetMicroflowIdAsync(string targetMicroflowId, CancellationToken cancellationToken)
        => _db.Deleteable<MicroflowReferenceEntity>()
            .Where(x => x.TargetMicroflowId == targetMicroflowId)
            .ExecuteCommandAsync(cancellationToken);

    private static ISugarQueryable<MicroflowReferenceEntity> ApplyReferenceFilters(
        ISugarQueryable<MicroflowReferenceEntity> q,
        MicroflowReferenceQuery query)
    {
        if (!query.IncludeInactive)
        {
            q = q.Where(x => x.Active);
        }

        var sourceTypes = query.SourceType.Where(static x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (sourceTypes.Length > 0)
        {
            q = q.Where(x => SqlFunc.ContainsArray(sourceTypes, x.SourceType));
        }

        var impactLevels = query.ImpactLevel.Where(static x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (impactLevels.Length > 0)
        {
            q = q.Where(x => SqlFunc.ContainsArray(impactLevels, x.ImpactLevel));
        }

        return q;
    }
}

public sealed class MicroflowRunRepository : IMicroflowRunRepository
{
    private readonly ISqlSugarClient _db;

    public MicroflowRunRepository(ISqlSugarClient db) => _db = db;

    public Task InsertSessionAsync(MicroflowRunSessionEntity entity, CancellationToken cancellationToken)
        => _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);

    public Task UpdateSessionAsync(MicroflowRunSessionEntity entity, CancellationToken cancellationToken)
        => _db.Updateable(entity).Where(x => x.Id == entity.Id).ExecuteCommandAsync(cancellationToken);

    public Task<MicroflowRunSessionEntity?> GetSessionAsync(string runId, CancellationToken cancellationToken)
        => _db.Queryable<MicroflowRunSessionEntity>().Where(x => x.Id == runId).FirstAsync(cancellationToken)!;

    public async Task<IReadOnlyList<MicroflowRunSessionEntity>> ListSessionsByResourceIdAsync(
        string resourceId,
        int pageIndex,
        int pageSize,
        IReadOnlyList<string>? statuses,
        CancellationToken cancellationToken)
    {
        var query = _db.Queryable<MicroflowRunSessionEntity>()
            .Where(x => x.ResourceId == resourceId);
        var normalizedStatuses = statuses?
            .Where(static status => !string.IsNullOrWhiteSpace(status))
            .Select(static status => status.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (normalizedStatuses is { Length: > 0 })
        {
            query = query.Where(x => SqlFunc.ContainsArray(normalizedStatuses, x.Status));
        }

        return await query
            .OrderBy(x => x.StartedAt, OrderByType.Desc)
            .ToPageListAsync(pageIndex <= 0 ? 1 : pageIndex, pageSize <= 0 ? 20 : pageSize, cancellationToken);
    }

    public Task<int> CountSessionsByResourceIdAsync(
        string resourceId,
        IReadOnlyList<string>? statuses,
        CancellationToken cancellationToken)
    {
        var query = _db.Queryable<MicroflowRunSessionEntity>()
            .Where(x => x.ResourceId == resourceId);
        var normalizedStatuses = statuses?
            .Where(static status => !string.IsNullOrWhiteSpace(status))
            .Select(static status => status.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (normalizedStatuses is { Length: > 0 })
        {
            query = query.Where(x => SqlFunc.ContainsArray(normalizedStatuses, x.Status));
        }

        return query.CountAsync(cancellationToken);
    }

    public Task InsertTraceFramesAsync(string runId, IReadOnlyList<MicroflowRunTraceFrameEntity> frames, CancellationToken cancellationToken)
    {
        foreach (var frame in frames)
        {
            frame.RunId = runId;
        }

        return InsertTraceFramesAsync(frames, cancellationToken);
    }

    public Task InsertTraceFramesAsync(IReadOnlyList<MicroflowRunTraceFrameEntity> frames, CancellationToken cancellationToken)
        => frames.Count == 0
            ? Task.CompletedTask
            : _db.Insertable(frames.ToList()).ExecuteCommandAsync(cancellationToken);

    public async Task<IReadOnlyList<MicroflowRunTraceFrameEntity>> ListTraceFramesAsync(string runId, CancellationToken cancellationToken)
        => await _db.Queryable<MicroflowRunTraceFrameEntity>()
            .Where(x => x.RunId == runId)
            .OrderBy(x => x.Sequence, OrderByType.Asc)
            .ToListAsync(cancellationToken);

    public Task InsertLogsAsync(string runId, IReadOnlyList<MicroflowRunLogEntity> logs, CancellationToken cancellationToken)
    {
        foreach (var log in logs)
        {
            log.RunId = runId;
        }

        return InsertLogsAsync(logs, cancellationToken);
    }

    public Task InsertLogsAsync(IReadOnlyList<MicroflowRunLogEntity> logs, CancellationToken cancellationToken)
        => logs.Count == 0
            ? Task.CompletedTask
            : _db.Insertable(logs.ToList()).ExecuteCommandAsync(cancellationToken);

    public async Task<IReadOnlyList<MicroflowRunLogEntity>> ListLogsAsync(string runId, CancellationToken cancellationToken)
        => await _db.Queryable<MicroflowRunLogEntity>()
            .Where(x => x.RunId == runId)
            .OrderBy(x => x.Timestamp, OrderByType.Asc)
            .ToListAsync(cancellationToken);

    public Task UpdateSessionStatusAsync(
        string runId,
        string status,
        DateTimeOffset? endedAt,
        CancellationToken cancellationToken)
        => _db.Updateable<MicroflowRunSessionEntity>()
            .SetColumns(x => x.Status == status)
            .SetColumns(x => x.EndedAt == endedAt)
            .Where(x => x.Id == runId)
            .ExecuteCommandAsync(cancellationToken);
}

public sealed class MicroflowMetadataCacheRepository : IMicroflowMetadataCacheRepository
{
    private readonly ISqlSugarClient _db;

    public MicroflowMetadataCacheRepository(ISqlSugarClient db) => _db = db;

    public Task<MicroflowMetadataCacheEntity?> GetLatestAsync(string? workspaceId, string? tenantId, CancellationToken cancellationToken)
    {
        var q = _db.Queryable<MicroflowMetadataCacheEntity>();
        if (!string.IsNullOrWhiteSpace(workspaceId))
        {
            q = q.Where(x => x.WorkspaceId == workspaceId);
        }

        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            q = q.Where(x => x.TenantId == tenantId);
        }

        return q.OrderBy(x => x.UpdatedAt, OrderByType.Desc).FirstAsync(cancellationToken)!;
    }

    public Task InsertAsync(MicroflowMetadataCacheEntity entity, CancellationToken cancellationToken)
        => _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);

    public Task<MicroflowMetadataCacheEntity?> GetByVersionAsync(string? workspaceId, string catalogVersion, CancellationToken cancellationToken)
    {
        var q = _db.Queryable<MicroflowMetadataCacheEntity>()
            .Where(x => x.CatalogVersion == catalogVersion);
        if (!string.IsNullOrWhiteSpace(workspaceId))
        {
            q = q.Where(x => x.WorkspaceId == workspaceId);
        }

        return q.OrderBy(x => x.UpdatedAt, OrderByType.Desc).FirstAsync(cancellationToken)!;
    }

    public async Task UpsertLatestAsync(MicroflowMetadataCacheEntity entity, CancellationToken cancellationToken)
    {
        var existing = await GetLatestAsync(entity.WorkspaceId, entity.TenantId, cancellationToken);
        if (existing is null)
        {
            await InsertAsync(entity, cancellationToken);
            return;
        }

        entity.Id = existing.Id;
        await _db.Updateable(entity).Where(x => x.Id == entity.Id).ExecuteCommandAsync(cancellationToken);
    }

    public Task<int> CountAsync(string? workspaceId, string? tenantId, CancellationToken cancellationToken)
    {
        var q = _db.Queryable<MicroflowMetadataCacheEntity>();
        if (!string.IsNullOrWhiteSpace(workspaceId))
        {
            q = q.Where(x => x.WorkspaceId == workspaceId);
        }

        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            q = q.Where(x => x.TenantId == tenantId);
        }

        return q.CountAsync(cancellationToken);
    }
}
