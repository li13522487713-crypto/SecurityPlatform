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
    {
        var q = _db.Queryable<MicroflowReferenceEntity>().Where(x => x.TargetMicroflowId == targetMicroflowId);
        if (!includeInactive)
        {
            q = q.Where(x => x.Active);
        }

        return await q.OrderBy(x => x.UpdatedAt, OrderByType.Desc).ToListAsync(cancellationToken);
    }

    public async Task UpsertReferencesAsync(
        string targetMicroflowId,
        IReadOnlyList<MicroflowReferenceEntity> references,
        CancellationToken cancellationToken)
    {
        await DeleteByTargetMicroflowIdAsync(targetMicroflowId, cancellationToken);
        if (references.Count > 0)
        {
            await _db.Insertable(references.ToList()).ExecuteCommandAsync(cancellationToken);
        }
    }

    public Task DeleteByTargetMicroflowIdAsync(string targetMicroflowId, CancellationToken cancellationToken)
        => _db.Deleteable<MicroflowReferenceEntity>()
            .Where(x => x.TargetMicroflowId == targetMicroflowId)
            .ExecuteCommandAsync(cancellationToken);
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

    public Task InsertTraceFramesAsync(IReadOnlyList<MicroflowRunTraceFrameEntity> frames, CancellationToken cancellationToken)
        => frames.Count == 0
            ? Task.CompletedTask
            : _db.Insertable(frames.ToList()).ExecuteCommandAsync(cancellationToken);

    public async Task<IReadOnlyList<MicroflowRunTraceFrameEntity>> ListTraceFramesAsync(string runId, CancellationToken cancellationToken)
        => await _db.Queryable<MicroflowRunTraceFrameEntity>()
            .Where(x => x.RunId == runId)
            .OrderBy(x => x.Sequence, OrderByType.Asc)
            .ToListAsync(cancellationToken);

    public Task InsertLogsAsync(IReadOnlyList<MicroflowRunLogEntity> logs, CancellationToken cancellationToken)
        => logs.Count == 0
            ? Task.CompletedTask
            : _db.Insertable(logs.ToList()).ExecuteCommandAsync(cancellationToken);

    public async Task<IReadOnlyList<MicroflowRunLogEntity>> ListLogsAsync(string runId, CancellationToken cancellationToken)
        => await _db.Queryable<MicroflowRunLogEntity>()
            .Where(x => x.RunId == runId)
            .OrderBy(x => x.Timestamp, OrderByType.Asc)
            .ToListAsync(cancellationToken);
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
}
