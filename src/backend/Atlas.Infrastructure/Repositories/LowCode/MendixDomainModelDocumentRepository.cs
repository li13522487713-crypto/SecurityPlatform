using Atlas.Application.Microflows.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories.LowCode;

public sealed class MendixDomainModelDocumentRepository : IMendixDomainModelDocumentRepository
{
    private readonly ISqlSugarClient _db;

    public MendixDomainModelDocumentRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public Task<MendixDomainModelDocument?> FindAsync(
        TenantId tenantId,
        long appId,
        string workspaceId,
        string moduleId,
        CancellationToken cancellationToken)
    {
        return _db.Queryable<MendixDomainModelDocument>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId && x.WorkspaceId == workspaceId && x.ModuleId == moduleId)
            .FirstAsync(cancellationToken)!;
    }

    public async Task<IReadOnlyList<MendixDomainModelDocument>> ListByAppAsync(
        TenantId tenantId,
        long appId,
        string workspaceId,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<MendixDomainModelDocument>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId && x.WorkspaceId == workspaceId)
            .OrderBy(x => x.ModuleId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MendixDomainModelDocument>> ListByWorkspaceModuleAsync(
        TenantId tenantId,
        string workspaceId,
        string moduleId,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<MendixDomainModelDocument>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.WorkspaceId == workspaceId && x.ModuleId == moduleId)
            .OrderByDescending(x => x.UpdatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<long> InsertAsync(MendixDomainModelDocument document, CancellationToken cancellationToken)
    {
        await _db.Insertable(document).ExecuteCommandAsync(cancellationToken);
        return document.Id;
    }

    public async Task<bool> UpdateAsync(MendixDomainModelDocument document, CancellationToken cancellationToken)
    {
        var rows = await _db.Updateable(document)
            .Where(x => x.Id == document.Id && x.TenantIdValue == document.TenantIdValue)
            .ExecuteCommandAsync(cancellationToken);
        return rows > 0;
    }
}
