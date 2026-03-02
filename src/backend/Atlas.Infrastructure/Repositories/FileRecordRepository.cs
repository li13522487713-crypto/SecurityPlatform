using Atlas.Domain.System.Entities;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class FileRecordRepository : RepositoryBase<FileRecord>
{
    public FileRecordRepository(ISqlSugarClient db) : base(db) { }

    public override async Task<FileRecord?> FindByIdAsync(TenantId tenantId, long id, CancellationToken ct)
    {
        return await Db.Queryable<FileRecord>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id && !x.IsDeleted)
            .FirstAsync(ct);
    }
}
