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

    public async Task<FileRecord?> FindByHashAsync(
        TenantId tenantId,
        string fileHashSha256,
        long sizeBytes,
        CancellationToken ct)
    {
        return await Db.Queryable<FileRecord>()
            .Where(x =>
                x.TenantIdValue == tenantId.Value
                && !x.IsDeleted
                && x.SizeBytes == sizeBytes
                && x.FileHashSha256 == fileHashSha256)
            .OrderByDescending(x => x.UploadedAt)
            .FirstAsync(ct);
    }

    /// <summary>查找指定文件名的最新版本记录。</summary>
    public async Task<FileRecord?> FindLatestByOriginalNameAsync(
        TenantId tenantId,
        string originalName,
        CancellationToken ct)
    {
        return await Db.Queryable<FileRecord>()
            .Where(x =>
                x.TenantIdValue == tenantId.Value
                && !x.IsDeleted
                && x.OriginalName == originalName
                && x.IsLatestVersion)
            .FirstAsync(ct);
    }

    /// <summary>批量查询同一文件名的所有版本（按版本号降序）。</summary>
    public async Task<List<FileRecord>> ListVersionsByOriginalNameAsync(
        TenantId tenantId,
        string originalName,
        CancellationToken ct)
    {
        return await Db.Queryable<FileRecord>()
            .Where(x =>
                x.TenantIdValue == tenantId.Value
                && !x.IsDeleted
                && x.OriginalName == originalName)
            .OrderByDescending(x => x.VersionNumber)
            .ToListAsync(ct);
    }

    /// <summary>以文件 ID 为基点，查询同一文件名的所有版本（按版本号降序）。</summary>
    public async Task<List<FileRecord>> ListVersionsByFileIdAsync(
        TenantId tenantId,
        long fileId,
        CancellationToken ct)
    {
        var record = await FindByIdAsync(tenantId, fileId, ct);
        if (record is null)
        {
            return [];
        }

        return await ListVersionsByOriginalNameAsync(tenantId, record.OriginalName, ct);
    }

    public override async Task<IReadOnlyList<FileRecord>> QueryByIdsAsync(
        TenantId tenantId,
        IReadOnlyList<long> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var idArray = ids.Distinct().ToArray();
        return await Db.Queryable<FileRecord>()
            .Where(x =>
                x.TenantIdValue == tenantId.Value
                && !x.IsDeleted
                && SqlFunc.ContainsArray(idArray, x.Id))
            .ToListAsync(cancellationToken);
    }
}
