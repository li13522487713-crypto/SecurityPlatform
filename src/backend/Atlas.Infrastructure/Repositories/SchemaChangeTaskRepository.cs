using Atlas.Application.DynamicTables.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.DynamicTables.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class SchemaChangeTaskRepository : ISchemaChangeTaskRepository
{
    private readonly ISqlSugarClient _db;

    public SchemaChangeTaskRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<SchemaChangeTask>> ListByAppInstanceAsync(
        TenantId tenantId,
        long appInstanceId,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<SchemaChangeTask>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppInstanceId == appInstanceId)
            .OrderByDescending(x => x.StartedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<SchemaChangeTask?> FindByIdAsync(
        TenantId tenantId,
        long taskId,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<SchemaChangeTask>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == taskId)
            .FirstAsync(cancellationToken);
    }

    public Task AddAsync(SchemaChangeTask task, CancellationToken cancellationToken)
    {
        return _db.Insertable(task).ExecuteCommandAsync(cancellationToken);
    }

    public Task UpdateAsync(SchemaChangeTask task, CancellationToken cancellationToken)
    {
        return _db.Updateable(task).ExecuteCommandAsync(cancellationToken);
    }
}
