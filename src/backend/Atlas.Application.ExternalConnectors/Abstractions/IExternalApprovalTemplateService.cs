using Atlas.Application.ExternalConnectors.Models;

namespace Atlas.Application.ExternalConnectors.Abstractions;

/// <summary>
/// 外部审批模板缓存与字段映射服务。
/// </summary>
public interface IExternalApprovalTemplateService
{
    Task<IReadOnlyList<ExternalApprovalTemplateResponse>> ListCachedAsync(long providerId, CancellationToken cancellationToken);

    /// <summary>
    /// 强制从外部 provider 拉取最新模板并写缓存。
    /// </summary>
    Task<ExternalApprovalTemplateResponse> RefreshAsync(long providerId, string externalTemplateId, CancellationToken cancellationToken);

    Task<ExternalApprovalTemplateMappingResponse?> GetMappingAsync(long providerId, long flowDefinitionId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ExternalApprovalTemplateMappingResponse>> ListMappingsAsync(long providerId, CancellationToken cancellationToken);

    Task<ExternalApprovalTemplateMappingResponse> UpsertMappingAsync(ExternalApprovalTemplateMappingRequest request, CancellationToken cancellationToken);

    Task DeleteMappingAsync(long mappingId, CancellationToken cancellationToken);
}
