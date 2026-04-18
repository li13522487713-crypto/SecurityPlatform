using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions.Knowledge;

/// <summary>
/// 知识库任务系统聚合查询门面（v5 §35/§37/§42 / 计划 G1）。
/// 负责跨类型 list / get / dead-letter / cancel；具体类型的 enqueue 由
/// <see cref="IKnowledgeParseJobService"/> / <see cref="IKnowledgeIndexJobService"/> 等子接口负责。
/// </summary>
public interface IKnowledgeJobService
{
    Task<PagedResult<KnowledgeJobDto>> ListAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        KnowledgeJobsListRequest request,
        CancellationToken cancellationToken);

    Task<PagedResult<KnowledgeJobDto>> ListAcrossKnowledgeBasesAsync(
        TenantId tenantId,
        KnowledgeJobsListRequest request,
        CancellationToken cancellationToken);

    Task<KnowledgeJobDto?> GetAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        long jobId,
        CancellationToken cancellationToken);

    Task<long> RerunParseAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        RerunParseRequest request,
        CancellationToken cancellationToken);

    Task<long> RebuildIndexAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        RebuildIndexRequest request,
        CancellationToken cancellationToken);

    Task RetryDeadLetterAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        long jobId,
        CancellationToken cancellationToken);

    /// <summary>
    /// 批量重投死信任务（v5 §42 / 计划 G5）。
    /// 留空 <c>JobIds</c> 时重投当前 KB 全部 DeadLetter。
    /// </summary>
    Task<int> RetryDeadLetterBatchAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        DeadLetterRetryRequest request,
        CancellationToken cancellationToken);

    Task CancelAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        long jobId,
        CancellationToken cancellationToken);
}

/// <summary>
/// 解析任务专用服务（v5 §35 / 计划 G1+G3）。
/// 由 DocumentService.CreateAsync / V5 controller 触发；底层调用 Hangfire BackgroundJob。
/// </summary>
public interface IKnowledgeParseJobService
{
    /// <summary>把"解析+切片+索引"作为一个 parse 任务持久化并入队 Hangfire。</summary>
    Task<long> EnqueueParseAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        long documentId,
        ParsingStrategy? parsingStrategy,
        CancellationToken cancellationToken);

    /// <summary>列出某文档的全部 parse 任务（v5 §35 documents/{id}/parse-jobs GET）。</summary>
    Task<IReadOnlyList<ParseJobDto>> ListByDocumentAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        long documentId,
        CancellationToken cancellationToken);

    /// <summary>重跑解析（v5 §35 documents/{id}/parse-jobs POST）。</summary>
    Task<long> ReplayAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        long documentId,
        ParseJobReplayRequest request,
        CancellationToken cancellationToken);
}

/// <summary>
/// 索引任务专用服务（v5 §35 / 计划 G1+G3+G6）。
/// </summary>
public interface IKnowledgeIndexJobService
{
    /// <summary>入队索引任务（chunking → embedding → vector store 写入）。</summary>
    Task<long> EnqueueIndexAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        long documentId,
        ChunkingProfile? chunkingProfile,
        KnowledgeIndexMode mode,
        CancellationToken cancellationToken);

    /// <summary>列出某文档的全部 index 任务（v5 §35 documents/{id}/index-jobs GET）。</summary>
    Task<IReadOnlyList<IndexJobDto>> ListByDocumentAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        long documentId,
        CancellationToken cancellationToken);

    /// <summary>对某文档重建索引（v5 §35 documents/{id}/index-jobs/rebuild POST）。</summary>
    Task<long> RebuildAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        long documentId,
        IndexJobRebuildRequest request,
        CancellationToken cancellationToken);
}
