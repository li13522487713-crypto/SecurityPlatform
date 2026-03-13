using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class KnowledgeDocumentRepository : RepositoryBase<KnowledgeDocument>
{
    public KnowledgeDocumentRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public async Task<(List<KnowledgeDocument> Items, long Total)> GetByKnowledgeBaseAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = Db.Queryable<KnowledgeDocument>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.KnowledgeBaseId == knowledgeBaseId);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.CreatedAt, OrderByType.Desc)
            .OrderBy(x => x.Id, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);
        return (items, total);
    }

    public async Task<List<KnowledgeDocument>> GetPendingAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        return await Db.Queryable<KnowledgeDocument>()
            .Where(x =>
                x.TenantIdValue == tenantId.Value &&
                (x.Status == DocumentProcessingStatus.Pending || x.Status == DocumentProcessingStatus.Processing))
            .OrderBy(x => x.CreatedAt, OrderByType.Asc)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<KnowledgeDocument>> GetAllByKnowledgeBaseAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        CancellationToken cancellationToken)
    {
        return await Db.Queryable<KnowledgeDocument>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.KnowledgeBaseId == knowledgeBaseId)
            .OrderBy(x => x.Id, OrderByType.Asc)
            .ToListAsync(cancellationToken);
    }

    public Task DeleteByKnowledgeBaseAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        CancellationToken cancellationToken)
    {
        return Db.Deleteable<KnowledgeDocument>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.KnowledgeBaseId == knowledgeBaseId)
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task<KnowledgeDocument?> FindByKnowledgeBaseAndIdAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        long documentId,
        CancellationToken cancellationToken)
    {
        return await Db.Queryable<KnowledgeDocument>()
            .Where(x =>
                x.TenantIdValue == tenantId.Value &&
                x.KnowledgeBaseId == knowledgeBaseId &&
                x.Id == documentId)
            .FirstAsync(cancellationToken);
    }

    public async Task<int> CountByKnowledgeBaseAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        CancellationToken cancellationToken)
    {
        return await Db.Queryable<KnowledgeDocument>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.KnowledgeBaseId == knowledgeBaseId)
            .CountAsync(cancellationToken);
    }
}
