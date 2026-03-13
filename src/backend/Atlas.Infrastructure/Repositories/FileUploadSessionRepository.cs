using Atlas.Core.Tenancy;
using Atlas.Domain.System.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class FileUploadSessionRepository : RepositoryBase<FileUploadSession>
{
    public FileUploadSessionRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public async Task<FileUploadSession?> FindActiveByIdAsync(
        TenantId tenantId,
        long sessionId,
        CancellationToken cancellationToken)
    {
        return await Db.Queryable<FileUploadSession>()
            .Where(x =>
                x.TenantIdValue == tenantId.Value
                && x.Id == sessionId
                && (x.Status == FileUploadSessionStatus.Pending || x.Status == FileUploadSessionStatus.Uploading))
            .FirstAsync(cancellationToken);
    }
}
