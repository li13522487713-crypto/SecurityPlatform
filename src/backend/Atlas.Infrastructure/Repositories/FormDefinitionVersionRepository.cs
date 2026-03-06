using Atlas.Application.LowCode.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class FormDefinitionVersionRepository : IFormDefinitionVersionRepository
{
    private readonly ISqlSugarClient _db;

    public FormDefinitionVersionRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<FormDefinitionVersion?> GetByIdAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken = default)
    {
        return await _db.Queryable<FormDefinitionVersion>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id)
            .FirstAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FormDefinitionVersion>> GetByFormDefinitionIdAsync(
        TenantId tenantId,
        long formDefinitionId,
        CancellationToken cancellationToken = default)
    {
        return await _db.Queryable<FormDefinitionVersion>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.FormDefinitionId == formDefinitionId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task InsertAsync(FormDefinitionVersion entity, CancellationToken cancellationToken = default)
    {
        return _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public Task DeleteByFormDefinitionIdAsync(
        TenantId tenantId,
        long formDefinitionId,
        CancellationToken cancellationToken = default)
    {
        return _db.Deleteable<FormDefinitionVersion>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.FormDefinitionId == formDefinitionId)
            .ExecuteCommandAsync(cancellationToken);
    }
}
