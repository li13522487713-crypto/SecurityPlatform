using Atlas.Application.Microflows.Abstractions;
using Atlas.Application.Microflows.Repositories;
using Atlas.Domain.Microflows.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories.Microflows;

public sealed class MicroflowResourceRepository : IMicroflowResourceRepository
{
    private readonly ISqlSugarClient _db;

    public MicroflowResourceRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public Task<MicroflowResourceEntity?> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        return _db.Queryable<MicroflowResourceEntity>()
            .Where(x => x.Id == id)
            .FirstAsync(cancellationToken)!;
    }

    public async Task<MicroflowResourceEntity?> GetByQualifiedNameAsync(string? workspaceId, string qualifiedName, CancellationToken cancellationToken)
    {
        var normalized = qualifiedName.Trim();
        var name = normalized.Contains('.', StringComparison.Ordinal)
            ? normalized[(normalized.LastIndexOf('.') + 1)..]
            : normalized;
        var q = _db.Queryable<MicroflowResourceEntity>().Where(x => x.Name == name);
        if (!string.IsNullOrWhiteSpace(workspaceId))
        {
            q = q.Where(x => x.WorkspaceId == workspaceId);
        }

        var candidates = await q.ToListAsync(cancellationToken);
        return candidates.FirstOrDefault(resource =>
            string.Equals(BuildQualifiedName(resource), normalized, StringComparison.OrdinalIgnoreCase)
            || string.Equals(resource.Name, normalized, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<IReadOnlyList<MicroflowResourceEntity>> ListAsync(
        MicroflowResourceQueryDto query,
        CancellationToken cancellationToken)
    {
        var pageIndex = query.PageIndex <= 0 ? 1 : query.PageIndex;
        var pageSize = query.PageSize <= 0 ? 20 : query.PageSize;

        return await ApplyOrdering(ApplyFilters(query), query.SortBy, query.SortOrder)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);
    }

    public Task<int> CountAsync(MicroflowResourceQueryDto query, CancellationToken cancellationToken)
    {
        return ApplyFilters(query).CountAsync(cancellationToken);
    }

    public Task InsertAsync(MicroflowResourceEntity entity, CancellationToken cancellationToken)
    {
        entity.CreatedAt = entity.CreatedAt == default ? DateTimeOffset.UtcNow : entity.CreatedAt;
        entity.UpdatedAt = entity.UpdatedAt == default ? entity.CreatedAt : entity.UpdatedAt;
        return _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public Task UpdateAsync(MicroflowResourceEntity entity, CancellationToken cancellationToken)
    {
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.ConcurrencyStamp = Guid.NewGuid().ToString("N");
        return _db.Updateable(entity).Where(x => x.Id == entity.Id).ExecuteCommandAsync(cancellationToken);
    }

    public Task UpdateManyAsync(IReadOnlyList<MicroflowResourceEntity> entities, CancellationToken cancellationToken)
    {
        if (entities.Count == 0)
        {
            return Task.CompletedTask;
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var entity in entities)
        {
            entity.UpdatedAt = now;
            entity.ConcurrencyStamp = Guid.NewGuid().ToString("N");
        }

        return _db.Updateable(entities.ToList()).ExecuteCommandAsync(cancellationToken);
    }

    public Task UpdateLastRunAsync(string id, string status, DateTimeOffset lastRunAt, CancellationToken cancellationToken)
    {
        return _db.Updateable<MicroflowResourceEntity>()
            .SetColumns(x => x.LastRunStatus == status)
            .SetColumns(x => x.LastRunAt == lastRunAt)
            .Where(x => x.Id == id)
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task UpdateReferenceCountsAsync(IReadOnlyDictionary<string, int> countsByResourceId, CancellationToken cancellationToken)
    {
        foreach (var (resourceId, count) in countsByResourceId)
        {
            await _db.Updateable<MicroflowResourceEntity>()
                .SetColumns(x => x.ReferenceCount == count)
                .Where(x => x.Id == resourceId)
                .ExecuteCommandAsync(cancellationToken);
        }
    }

    public Task DeleteAsync(string id, CancellationToken cancellationToken)
    {
        return _db.Deleteable<MicroflowResourceEntity>().Where(x => x.Id == id).ExecuteCommandAsync(cancellationToken);
    }

    public Task<bool> ExistsByNameAsync(string? workspaceId, string name, CancellationToken cancellationToken)
    {
        return ExistsByNameAsync(workspaceId, name, null, cancellationToken);
    }

    public Task<bool> ExistsByNameAsync(string? workspaceId, string name, string? excludeId, CancellationToken cancellationToken)
    {
        var q = _db.Queryable<MicroflowResourceEntity>().Where(x => x.Name == name);
        if (!string.IsNullOrWhiteSpace(workspaceId))
        {
            q = q.Where(x => x.WorkspaceId == workspaceId);
        }

        if (!string.IsNullOrWhiteSpace(excludeId))
        {
            q = q.Where(x => x.Id != excludeId);
        }

        return q.AnyAsync();
    }

    private ISugarQueryable<MicroflowResourceEntity> ApplyFilters(MicroflowResourceQueryDto query)
    {
        var q = _db.Queryable<MicroflowResourceEntity>();

        if (!string.IsNullOrWhiteSpace(query.WorkspaceId))
        {
            q = q.Where(x => x.WorkspaceId == query.WorkspaceId);
        }

        if (!string.IsNullOrWhiteSpace(query.TenantId))
        {
            q = q.Where(x => x.TenantId == query.TenantId);
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            q = q.Where(x => x.Name.Contains(keyword) || x.DisplayName.Contains(keyword) || x.Description!.Contains(keyword));
        }

        if (query.Status.Count > 0)
        {
            var statuses = query.Status.Where(static x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (statuses.Length > 0)
            {
                q = q.Where(x => SqlFunc.ContainsArray(statuses, x.Status));
            }
        }

        if (query.PublishStatus.Count > 0)
        {
            var statuses = query.PublishStatus.Where(static x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (statuses.Length > 0)
            {
                q = q.Where(x => SqlFunc.ContainsArray(statuses, x.PublishStatus));
            }
        }

        if (query.FavoriteOnly)
        {
            q = q.Where(x => x.Favorite);
        }

        if (!string.IsNullOrWhiteSpace(query.OwnerId))
        {
            q = q.Where(x => x.OwnerId == query.OwnerId);
        }

        if (!string.IsNullOrWhiteSpace(query.ModuleId))
        {
            q = q.Where(x => x.ModuleId == query.ModuleId);
        }

        if (query.FolderId is not null)
        {
            q = string.IsNullOrWhiteSpace(query.FolderId)
                ? q.Where(x => x.FolderId == null || x.FolderId == "")
                : q.Where(x => x.FolderId == query.FolderId);
        }

        if (query.UpdatedFrom.HasValue)
        {
            q = q.Where(x => x.UpdatedAt >= query.UpdatedFrom.Value);
        }

        if (query.UpdatedTo.HasValue)
        {
            q = q.Where(x => x.UpdatedAt <= query.UpdatedTo.Value);
        }

        var tagTokens = query.Tags
            .Where(static x => !string.IsNullOrWhiteSpace(x))
            .Select(static x => $"\"{x.Trim()}\"")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (tagTokens.Length > 0)
        {
            var expression = Expressionable.Create<MicroflowResourceEntity>();
            foreach (var token in tagTokens)
            {
                expression.Or(x => x.TagsJson.Contains(token));
            }

            q = q.Where(expression.ToExpression());
        }

        q = q.Where(x => x.ExtraJson == null || !x.ExtraJson.Contains("\"deleted\":true"));

        return q;
    }

    public async Task<IReadOnlyList<MicroflowResourceEntity>> ListByFolderIdAsync(string folderId, CancellationToken cancellationToken)
    {
        return await _db.Queryable<MicroflowResourceEntity>()
            .Where(x => x.FolderId == folderId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MicroflowResourceEntity>> ListByFolderIdsAsync(IReadOnlyList<string> folderIds, CancellationToken cancellationToken)
    {
        var ids = folderIds.Where(static id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToArray();
        if (ids.Length == 0)
        {
            return Array.Empty<MicroflowResourceEntity>();
        }

        return await _db.Queryable<MicroflowResourceEntity>()
            .Where(x => SqlFunc.ContainsArray(ids, x.FolderId))
            .ToListAsync(cancellationToken);
    }

    private static string BuildQualifiedName(MicroflowResourceEntity resource)
    {
        var moduleName = string.IsNullOrWhiteSpace(resource.ModuleName) ? resource.ModuleId : resource.ModuleName!;
        return $"{moduleName}.{resource.Name}";
    }

    private static ISugarQueryable<MicroflowResourceEntity> ApplyOrdering(
        ISugarQueryable<MicroflowResourceEntity> query,
        string? sortBy,
        string? sortOrder)
    {
        var orderByType = IsAscending(sortOrder) ? OrderByType.Asc : OrderByType.Desc;
        return sortBy switch
        {
            "name" => query.OrderBy(x => x.Name, orderByType),
            "createdAt" => query.OrderBy(x => x.CreatedAt, orderByType),
            "version" => query.OrderBy(x => x.Version, orderByType),
            "referenceCount" => query.OrderBy(x => x.ReferenceCount, orderByType),
            _ => query.OrderBy(x => x.UpdatedAt, orderByType)
        };
    }

    private static bool IsAscending(string? sortOrder)
    {
        return string.Equals(sortOrder, "asc", StringComparison.OrdinalIgnoreCase);
    }
}
