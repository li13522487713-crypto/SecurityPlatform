using Atlas.Application.Microflows.Repositories;
using Atlas.Domain.Microflows.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories.Microflows;

public sealed class MicroflowFolderRepository : IMicroflowFolderRepository
{
    private readonly ISqlSugarClient _db;

    public MicroflowFolderRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public Task<MicroflowFolderEntity?> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        return _db.Queryable<MicroflowFolderEntity>()
            .Where(x => x.Id == id)
            .FirstAsync(cancellationToken)!;
    }

    public async Task<IReadOnlyList<MicroflowFolderEntity>> ListByModuleAsync(
        string? workspaceId,
        string? tenantId,
        string moduleId,
        CancellationToken cancellationToken)
    {
        var q = _db.Queryable<MicroflowFolderEntity>().Where(x => x.ModuleId == moduleId);
        if (!string.IsNullOrWhiteSpace(workspaceId))
        {
            q = q.Where(x => x.WorkspaceId == workspaceId);
        }

        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            q = q.Where(x => x.TenantId == tenantId);
        }

        return await q
            .OrderBy(x => x.Path, OrderByType.Asc)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> ExistsBySiblingNameAsync(
        string? workspaceId,
        string? tenantId,
        string moduleId,
        string? parentFolderId,
        string name,
        string? excludeId,
        CancellationToken cancellationToken)
    {
        var q = _db.Queryable<MicroflowFolderEntity>()
            .Where(x => x.ModuleId == moduleId && x.Name == name);
        q = string.IsNullOrWhiteSpace(parentFolderId)
            ? q.Where(x => x.ParentFolderId == null || x.ParentFolderId == "")
            : q.Where(x => x.ParentFolderId == parentFolderId);
        if (!string.IsNullOrWhiteSpace(workspaceId))
        {
            q = q.Where(x => x.WorkspaceId == workspaceId);
        }

        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            q = q.Where(x => x.TenantId == tenantId);
        }

        if (!string.IsNullOrWhiteSpace(excludeId))
        {
            q = q.Where(x => x.Id != excludeId);
        }

        return q.AnyAsync();
    }

    public Task InsertAsync(MicroflowFolderEntity entity, CancellationToken cancellationToken)
    {
        entity.CreatedAt = entity.CreatedAt == default ? DateTimeOffset.UtcNow : entity.CreatedAt;
        entity.UpdatedAt = entity.UpdatedAt == default ? entity.CreatedAt : entity.UpdatedAt;
        return _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public Task UpdateAsync(MicroflowFolderEntity entity, CancellationToken cancellationToken)
    {
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        return _db.Updateable(entity).Where(x => x.Id == entity.Id).ExecuteCommandAsync(cancellationToken);
    }

    public Task UpdateManyAsync(IReadOnlyList<MicroflowFolderEntity> entities, CancellationToken cancellationToken)
    {
        return entities.Count == 0
            ? Task.CompletedTask
            : _db.Updateable(entities.ToList()).ExecuteCommandAsync(cancellationToken);
    }

    public Task DeleteAsync(string id, CancellationToken cancellationToken)
    {
        return _db.Deleteable<MicroflowFolderEntity>().Where(x => x.Id == id).ExecuteCommandAsync(cancellationToken);
    }
}
