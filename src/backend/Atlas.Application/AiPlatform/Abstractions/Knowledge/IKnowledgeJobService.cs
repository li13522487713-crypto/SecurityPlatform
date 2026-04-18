using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions.Knowledge;

/// <summary>
/// 知识库任务系统：v5 §35/§37/§42 报告要求把"上传/解析/索引/重建/GC"独立成 job 实体并支持死信重投。
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

    Task CancelAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        long jobId,
        CancellationToken cancellationToken);
}
