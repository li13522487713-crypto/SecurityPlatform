using Atlas.Core.Tenancy;
using Atlas.Domain.System.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class FileTusUploadSessionRepository : RepositoryBase<FileTusUploadSession>
{
    public FileTusUploadSessionRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public async Task<FileTusUploadSession?> FindActiveByIdAsync(
        TenantId tenantId,
        long sessionId,
        CancellationToken cancellationToken)
    {
        return await Db.Queryable<FileTusUploadSession>()
            .Where(x =>
                x.TenantIdValue == tenantId.Value
                && x.Id == sessionId
                && (x.Status == FileTusUploadSessionStatus.Pending || x.Status == FileTusUploadSessionStatus.Uploading))
            .FirstAsync(cancellationToken);
    }
}
