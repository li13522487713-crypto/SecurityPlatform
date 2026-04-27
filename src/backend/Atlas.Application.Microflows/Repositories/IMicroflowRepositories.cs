using Atlas.Application.Microflows.Abstractions;
using Atlas.Domain.Microflows.Entities;

namespace Atlas.Application.Microflows.Repositories;

public interface IMicroflowResourceRepository
{
    Task<MicroflowResourceEntity?> GetByIdAsync(string id, CancellationToken cancellationToken);

    Task<IReadOnlyList<MicroflowResourceEntity>> ListAsync(MicroflowResourceQueryDto query, CancellationToken cancellationToken);

    Task<int> CountAsync(MicroflowResourceQueryDto query, CancellationToken cancellationToken);

    Task InsertAsync(MicroflowResourceEntity entity, CancellationToken cancellationToken);

    Task UpdateAsync(MicroflowResourceEntity entity, CancellationToken cancellationToken);

    Task DeleteAsync(string id, CancellationToken cancellationToken);

    Task<bool> ExistsByNameAsync(string? workspaceId, string name, CancellationToken cancellationToken);

    Task<bool> ExistsByNameAsync(string? workspaceId, string name, string? excludeId, CancellationToken cancellationToken);
}

public interface IMicroflowSchemaSnapshotRepository
{
    Task<MicroflowSchemaSnapshotEntity?> GetByIdAsync(string id, CancellationToken cancellationToken);

    Task<IReadOnlyList<MicroflowSchemaSnapshotEntity>> ListByResourceIdAsync(string resourceId, CancellationToken cancellationToken);

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

    Task UpsertReferencesAsync(string targetMicroflowId, IReadOnlyList<MicroflowReferenceEntity> references, CancellationToken cancellationToken);

    Task DeleteByTargetMicroflowIdAsync(string targetMicroflowId, CancellationToken cancellationToken);
}

public interface IMicroflowRunRepository
{
    Task InsertSessionAsync(MicroflowRunSessionEntity entity, CancellationToken cancellationToken);

    Task UpdateSessionAsync(MicroflowRunSessionEntity entity, CancellationToken cancellationToken);

    Task<MicroflowRunSessionEntity?> GetSessionAsync(string runId, CancellationToken cancellationToken);

    Task InsertTraceFramesAsync(IReadOnlyList<MicroflowRunTraceFrameEntity> frames, CancellationToken cancellationToken);

    Task<IReadOnlyList<MicroflowRunTraceFrameEntity>> ListTraceFramesAsync(string runId, CancellationToken cancellationToken);

    Task InsertLogsAsync(IReadOnlyList<MicroflowRunLogEntity> logs, CancellationToken cancellationToken);

    Task<IReadOnlyList<MicroflowRunLogEntity>> ListLogsAsync(string runId, CancellationToken cancellationToken);
}

public interface IMicroflowMetadataCacheRepository
{
    Task<MicroflowMetadataCacheEntity?> GetLatestAsync(string? workspaceId, string? tenantId, CancellationToken cancellationToken);

    Task InsertAsync(MicroflowMetadataCacheEntity entity, CancellationToken cancellationToken);

    Task<MicroflowMetadataCacheEntity?> GetByVersionAsync(string? workspaceId, string catalogVersion, CancellationToken cancellationToken);
}

public interface IMicroflowStorageTransaction
{
    Task ExecuteAsync(Func<Task> operation, CancellationToken cancellationToken);
}
