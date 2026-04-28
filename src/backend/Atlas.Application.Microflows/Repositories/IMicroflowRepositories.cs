using Atlas.Application.Microflows.Abstractions;
using Atlas.Domain.Microflows.Entities;

namespace Atlas.Application.Microflows.Repositories;

public interface IMicroflowResourceRepository
{
    Task<MicroflowResourceEntity?> GetByIdAsync(string id, CancellationToken cancellationToken);

    Task<MicroflowResourceEntity?> GetByQualifiedNameAsync(string? workspaceId, string qualifiedName, CancellationToken cancellationToken);

    Task<IReadOnlyList<MicroflowResourceEntity>> ListAsync(MicroflowResourceQueryDto query, CancellationToken cancellationToken);

    Task<int> CountAsync(MicroflowResourceQueryDto query, CancellationToken cancellationToken);

    Task InsertAsync(MicroflowResourceEntity entity, CancellationToken cancellationToken);

    Task UpdateAsync(MicroflowResourceEntity entity, CancellationToken cancellationToken);

    Task UpdateManyAsync(IReadOnlyList<MicroflowResourceEntity> entities, CancellationToken cancellationToken);

    Task UpdateLastRunAsync(string id, string status, DateTimeOffset lastRunAt, CancellationToken cancellationToken);

    Task UpdateReferenceCountsAsync(IReadOnlyDictionary<string, int> countsByResourceId, CancellationToken cancellationToken);

    Task DeleteAsync(string id, CancellationToken cancellationToken);

    Task<bool> ExistsByNameAsync(string? workspaceId, string name, CancellationToken cancellationToken);

    Task<bool> ExistsByNameAsync(string? workspaceId, string name, string? excludeId, CancellationToken cancellationToken);

    Task<IReadOnlyList<MicroflowResourceEntity>> ListByFolderIdAsync(string folderId, CancellationToken cancellationToken);

    Task<IReadOnlyList<MicroflowResourceEntity>> ListByFolderIdsAsync(IReadOnlyList<string> folderIds, CancellationToken cancellationToken);
}

public interface IMicroflowFolderRepository
{
    Task<MicroflowFolderEntity?> GetByIdAsync(string id, CancellationToken cancellationToken);

    Task<IReadOnlyList<MicroflowFolderEntity>> ListByModuleAsync(string? workspaceId, string? tenantId, string moduleId, CancellationToken cancellationToken);

    Task<bool> ExistsBySiblingNameAsync(string? workspaceId, string? tenantId, string moduleId, string? parentFolderId, string name, string? excludeId, CancellationToken cancellationToken);

    Task InsertAsync(MicroflowFolderEntity entity, CancellationToken cancellationToken);

    Task UpdateAsync(MicroflowFolderEntity entity, CancellationToken cancellationToken);

    Task UpdateManyAsync(IReadOnlyList<MicroflowFolderEntity> entities, CancellationToken cancellationToken);

    Task DeleteAsync(string id, CancellationToken cancellationToken);
}

public interface IMicroflowSchemaSnapshotRepository
{
    Task<MicroflowSchemaSnapshotEntity?> GetByIdAsync(string id, CancellationToken cancellationToken);

    Task<IReadOnlyList<MicroflowSchemaSnapshotEntity>> ListByResourceIdAsync(string resourceId, CancellationToken cancellationToken);

    Task<IReadOnlyList<MicroflowSchemaSnapshotEntity>> ListByIdsAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken);

    Task InsertAsync(MicroflowSchemaSnapshotEntity entity, CancellationToken cancellationToken);

    Task<MicroflowSchemaSnapshotEntity?> GetLatestByResourceIdAsync(string resourceId, CancellationToken cancellationToken);
}

public interface IMicroflowVersionRepository
{
    Task<IReadOnlyList<MicroflowVersionEntity>> ListByResourceIdAsync(string resourceId, CancellationToken cancellationToken);

    Task<MicroflowVersionEntity?> GetByIdAsync(string id, CancellationToken cancellationToken);

    Task<MicroflowVersionEntity?> GetByResourceVersionAsync(string resourceId, string version, CancellationToken cancellationToken);

    Task InsertAsync(MicroflowVersionEntity entity, CancellationToken cancellationToken);

    Task MarkLatestPublishedAsync(string resourceId, string versionId, CancellationToken cancellationToken);
}

public interface IMicroflowPublishSnapshotRepository
{
    Task<MicroflowPublishSnapshotEntity?> GetByIdAsync(string id, CancellationToken cancellationToken);

    Task<MicroflowPublishSnapshotEntity?> GetLatestByResourceIdAsync(string resourceId, CancellationToken cancellationToken);

    Task<MicroflowPublishSnapshotEntity?> GetByResourceVersionAsync(string resourceId, string version, CancellationToken cancellationToken);

    Task InsertAsync(MicroflowPublishSnapshotEntity entity, CancellationToken cancellationToken);
}

public interface IMicroflowReferenceRepository
{
    Task<IReadOnlyList<MicroflowReferenceEntity>> ListByTargetMicroflowIdAsync(string targetMicroflowId, bool includeInactive, CancellationToken cancellationToken);

    Task<IReadOnlyList<MicroflowReferenceEntity>> ListByTargetMicroflowIdAsync(
        string targetMicroflowId,
        MicroflowReferenceQuery query,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<MicroflowReferenceEntity>> ListBySourceAsync(string sourceType, string sourceId, CancellationToken cancellationToken);

    Task UpsertReferencesAsync(string targetMicroflowId, IReadOnlyList<MicroflowReferenceEntity> references, CancellationToken cancellationToken);

    Task UpsertReferencesForSourceAsync(string sourceType, string sourceId, IReadOnlyList<MicroflowReferenceEntity> references, CancellationToken cancellationToken);

    Task InsertManyAsync(IReadOnlyList<MicroflowReferenceEntity> references, CancellationToken cancellationToken);

    Task<int> CountByTargetMicroflowIdAsync(string targetMicroflowId, MicroflowReferenceQuery query, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, int>> CountByTargetMicroflowIdsAsync(
        IReadOnlyList<string> targetMicroflowIds,
        MicroflowReferenceQuery query,
        CancellationToken cancellationToken);

    Task DeleteBySourceAsync(string sourceType, string sourceId, CancellationToken cancellationToken);

    Task DeleteByTargetMicroflowIdAsync(string targetMicroflowId, CancellationToken cancellationToken);
}

public sealed record MicroflowReferenceQuery
{
    public bool IncludeInactive { get; init; }

    public IReadOnlyList<string> SourceType { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> ImpactLevel { get; init; } = Array.Empty<string>();
}

public interface IMicroflowRunRepository
{
    Task InsertSessionAsync(MicroflowRunSessionEntity entity, CancellationToken cancellationToken);

    Task UpdateSessionAsync(MicroflowRunSessionEntity entity, CancellationToken cancellationToken);

    Task<MicroflowRunSessionEntity?> GetSessionAsync(string runId, CancellationToken cancellationToken);

    Task<IReadOnlyList<MicroflowRunSessionEntity>> ListSessionsByResourceIdAsync(
        string resourceId,
        int pageIndex,
        int pageSize,
        IReadOnlyList<string>? statuses,
        CancellationToken cancellationToken);

    Task<int> CountSessionsByResourceIdAsync(
        string resourceId,
        IReadOnlyList<string>? statuses,
        CancellationToken cancellationToken);

    Task InsertTraceFramesAsync(
        string runId,
        IReadOnlyList<MicroflowRunTraceFrameEntity> frames,
        CancellationToken cancellationToken);

    Task InsertTraceFramesAsync(IReadOnlyList<MicroflowRunTraceFrameEntity> frames, CancellationToken cancellationToken);

    Task<IReadOnlyList<MicroflowRunTraceFrameEntity>> ListTraceFramesAsync(string runId, CancellationToken cancellationToken);

    Task InsertLogsAsync(
        string runId,
        IReadOnlyList<MicroflowRunLogEntity> logs,
        CancellationToken cancellationToken);

    Task InsertLogsAsync(IReadOnlyList<MicroflowRunLogEntity> logs, CancellationToken cancellationToken);

    Task<IReadOnlyList<MicroflowRunLogEntity>> ListLogsAsync(string runId, CancellationToken cancellationToken);

    Task UpdateSessionStatusAsync(
        string runId,
        string status,
        DateTimeOffset? endedAt,
        CancellationToken cancellationToken);
}

public interface IMicroflowMetadataCacheRepository
{
    Task<MicroflowMetadataCacheEntity?> GetLatestAsync(string? workspaceId, string? tenantId, CancellationToken cancellationToken);

    Task InsertAsync(MicroflowMetadataCacheEntity entity, CancellationToken cancellationToken);

    Task<MicroflowMetadataCacheEntity?> GetByVersionAsync(string? workspaceId, string catalogVersion, CancellationToken cancellationToken);

    Task UpsertLatestAsync(MicroflowMetadataCacheEntity entity, CancellationToken cancellationToken);

    Task<int> CountAsync(string? workspaceId, string? tenantId, CancellationToken cancellationToken);
}

public interface IMicroflowStorageTransaction
{
    Task ExecuteAsync(Func<Task> operation, CancellationToken cancellationToken);
}
