using Atlas.Domain.System.Entities;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class AttachmentBindingRepository : RepositoryBase<AttachmentBinding>
{
    public AttachmentBindingRepository(ISqlSugarClient db) : base(db) { }

    public override async Task<AttachmentBinding?> FindByIdAsync(TenantId tenantId, long id, CancellationToken ct)
    {
        return await Db.Queryable<AttachmentBinding>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id)
            .FirstAsync(ct);
    }

    /// <summary>批量查询指定业务实体的所有附件绑定（可按 FieldKey 过滤）。</summary>
    public async Task<List<AttachmentBinding>> ListByEntityAsync(
        TenantId tenantId,
        string entityType,
        long entityId,
        string? fieldKey,
        CancellationToken ct)
    {
        var query = Db.Queryable<AttachmentBinding>()
            .Where(x =>
                x.TenantIdValue == tenantId.Value
                && x.EntityType == entityType
                && x.EntityId == entityId);

        if (!string.IsNullOrWhiteSpace(fieldKey))
        {
            query = query.Where(x => x.FieldKey == fieldKey);
        }

        return await query.ToListAsync(ct);
    }

    /// <summary>查找指定文件、业务实体、字段槽的绑定记录。</summary>
    public async Task<AttachmentBinding?> FindBindingAsync(
        TenantId tenantId,
        long fileRecordId,
        string entityType,
        long entityId,
        string? fieldKey,
        CancellationToken ct)
    {
        var query = Db.Queryable<AttachmentBinding>()
            .Where(x =>
                x.TenantIdValue == tenantId.Value
                && x.FileRecordId == fileRecordId
                && x.EntityType == entityType
                && x.EntityId == entityId);

        if (fieldKey is not null)
        {
            query = query.Where(x => x.FieldKey == fieldKey);
        }

        return await query.FirstAsync(ct);
    }

    /// <summary>批量查询多个文件 ID 的绑定记录（用于级联删除，避免循环查库）。</summary>
    public async Task<List<AttachmentBinding>> ListByFileIdsAsync(
        TenantId tenantId,
        IReadOnlyList<long> fileRecordIds,
        CancellationToken ct)
    {
        if (fileRecordIds.Count == 0)
        {
            return [];
        }

        var ids = fileRecordIds.ToArray();
        return await Db.Queryable<AttachmentBinding>()
            .Where(x =>
                x.TenantIdValue == tenantId.Value
                && SqlFunc.ContainsArray(ids, x.FileRecordId))
            .ToListAsync(ct);
    }
}
