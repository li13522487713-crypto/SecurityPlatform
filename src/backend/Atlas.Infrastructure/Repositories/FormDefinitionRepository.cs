using Atlas.Application.LowCode.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class FormDefinitionRepository : IFormDefinitionRepository
{
    private readonly ISqlSugarClient _db;

    public FormDefinitionRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<FormDefinition?> GetByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken = default)
    {
        return await _db.Queryable<FormDefinition>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id)
            .FirstAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FormDefinition>> GetAllAsync(TenantId tenantId, CancellationToken cancellationToken = default)
    {
        return await _db.Queryable<FormDefinition>()
            .Where(x => x.TenantIdValue == tenantId.Value)
            .OrderBy(x => x.UpdatedAt, OrderByType.Desc)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<FormDefinition> Items, int Total)> GetPagedAsync(
        TenantId tenantId, int pageIndex, int pageSize, string? keyword, string? category,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Queryable<FormDefinition>()
            .Where(x => x.TenantIdValue == tenantId.Value);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x => x.Name.Contains(keyword) || (x.Description != null && x.Description.Contains(keyword)));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(x => x.Category == category);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.UpdatedAt, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);

        return (items, total);
    }

    public Task InsertAsync(FormDefinition entity, CancellationToken cancellationToken = default)
    {
        return _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public Task UpdateAsync(FormDefinition entity, CancellationToken cancellationToken = default)
    {
        return _db.Updateable(entity)
            .Where(x => x.Id == entity.Id && x.TenantIdValue == entity.TenantIdValue)
            .ExecuteCommandAsync(cancellationToken);
    }

    public Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        return _db.Deleteable<FormDefinition>()
            .Where(x => x.Id == id)
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task<bool> ExistsByNameAsync(TenantId tenantId, string name, long? excludeId = null, CancellationToken cancellationToken = default)
    {
        var query = _db.Queryable<FormDefinition>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Name == name);

        if (excludeId.HasValue)
        {
            query = query.Where(x => x.Id != excludeId.Value);
        }

        return await query.AnyAsync();
    }
}
