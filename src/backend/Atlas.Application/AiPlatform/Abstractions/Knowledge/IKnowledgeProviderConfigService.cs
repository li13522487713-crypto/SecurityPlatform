using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions.Knowledge;

/// <summary>
/// Provider 配置中心：v5 §39/§42 要求把 upload / storage / vector / embedding / generation
/// 五类 Provider 抽象成 adapter，并在前端集中配置查看。
/// </summary>
public interface IKnowledgeProviderConfigService
{
    Task<IReadOnlyList<KnowledgeProviderConfigDto>> ListAsync(
        TenantId tenantId,
        CancellationToken cancellationToken);

    /// <summary>v5 §39 / 计划 G1+G5：管理员通过 PUT /provider-configs/{role} 写入或更新该 role 的默认 provider。</summary>
    Task<KnowledgeProviderConfigDto> UpsertAsync(
        TenantId tenantId,
        KnowledgeProviderConfigUpsertRequest request,
        CancellationToken cancellationToken);
}
