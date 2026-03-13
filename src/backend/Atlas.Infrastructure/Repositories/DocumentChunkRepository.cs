using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class DocumentChunkRepository : RepositoryBase<DocumentChunk>
{
    public DocumentChunkRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public async Task<(List<DocumentChunk> Items, long Total)> GetByDocumentAsync(
        TenantId tenantId,
        long documentId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = Db.Queryable<DocumentChunk>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.DocumentId == documentId);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.ChunkIndex, OrderByType.Asc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);
        return (items, total);
    }

    public async Task<List<DocumentChunk>> GetByKnowledgeBaseAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        int top = 200,
        CancellationToken cancellationToken = default)
    {
        var query = Db.Queryable<DocumentChunk>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.KnowledgeBaseId == knowledgeBaseId)
            .OrderBy(x => x.Id, OrderByType.Desc);

        if (top > 0)
        {
            query = query.Take(top);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public Task DeleteByDocumentAsync(TenantId tenantId, long documentId, CancellationToken cancellationToken)
    {
        return Db.Deleteable<DocumentChunk>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.DocumentId == documentId)
            .ExecuteCommandAsync(cancellationToken);
    }

    public Task DeleteByKnowledgeBaseAsync(TenantId tenantId, long knowledgeBaseId, CancellationToken cancellationToken)
    {
        return Db.Deleteable<DocumentChunk>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.KnowledgeBaseId == knowledgeBaseId)
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task<List<DocumentChunk>> GetAllByDocumentAsync(
        TenantId tenantId,
        long documentId,
        CancellationToken cancellationToken)
    {
        return await Db.Queryable<DocumentChunk>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.DocumentId == documentId)
            .OrderBy(x => x.ChunkIndex, OrderByType.Asc)
            .ToListAsync(cancellationToken);
    }

    public async Task<DocumentChunk?> FindByKnowledgeBaseAndIdAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        long chunkId,
        CancellationToken cancellationToken)
    {
        return await Db.Queryable<DocumentChunk>()
            .Where(x =>
                x.TenantIdValue == tenantId.Value &&
                x.KnowledgeBaseId == knowledgeBaseId &&
                x.Id == chunkId)
            .FirstAsync(cancellationToken);
    }

    public Task AddRangeAsync(IReadOnlyCollection<DocumentChunk> entities, CancellationToken cancellationToken)
    {
        if (entities.Count == 0)
        {
            return Task.CompletedTask;
        }

        return Db.Insertable(entities.ToList()).ExecuteCommandAsync(cancellationToken);
    }

    public Task MarkEmbeddingByIdsAsync(
        TenantId tenantId,
        IReadOnlyCollection<long> chunkIds,
        bool hasEmbedding,
        CancellationToken cancellationToken)
    {
        if (chunkIds.Count == 0)
        {
            return Task.CompletedTask;
        }

        var idArray = chunkIds.Distinct().ToArray();
        return Db.Updateable<DocumentChunk>()
            .SetColumns(x => x.HasEmbedding == hasEmbedding)
            .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(idArray, x.Id))
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task<int> CountByKnowledgeBaseAsync(TenantId tenantId, long knowledgeBaseId, CancellationToken cancellationToken)
    {
        return await Db.Queryable<DocumentChunk>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.KnowledgeBaseId == knowledgeBaseId)
            .CountAsync(cancellationToken);
    }
}
