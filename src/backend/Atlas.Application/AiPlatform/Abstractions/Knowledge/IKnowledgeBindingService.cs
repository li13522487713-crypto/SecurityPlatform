using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions.Knowledge;

/// <summary>
/// 知识库绑定关系：Agent / App / Workflow / Chatflow → KnowledgeBase。
/// 删除 KB 前必须列出现有绑定，依赖检查见 v5 §39。
/// </summary>
public interface IKnowledgeBindingService
{
    Task<PagedResult<KnowledgeBindingDto>> ListAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken);

    Task<PagedResult<KnowledgeBindingDto>> ListAllAsync(
        TenantId tenantId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken);

    /// <summary>v5 §39 / 计划 G1：单条绑定详情；前端"绑定关系图谱"详情侧栏使用。</summary>
    Task<KnowledgeBindingDto?> GetByIdAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        long bindingId,
        CancellationToken cancellationToken);

    Task<long> CreateAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        KnowledgeBindingCreateRequest request,
        CancellationToken cancellationToken);

    Task RemoveAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        long bindingId,
        CancellationToken cancellationToken);
}
