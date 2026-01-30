using Atlas.Application.Visualization.Models;
using Atlas.Core.Models;
using Atlas.Application.Audit.Models;
using Atlas.Domain.Approval.Enums;

namespace Atlas.Application.Visualization.Abstractions;

/// <summary>
/// 可视化中心查询与操作契约（骨架版，后续可替换为真实实现）
/// </summary>
public interface IVisualizationQueryService
{
    Task<VisualizationOverviewResponse> GetOverviewAsync(VisualizationFilterRequest filter, CancellationToken cancellationToken);

    Task<PagedResult<VisualizationProcessSummary>> GetProcessesAsync(PagedRequest request, CancellationToken cancellationToken);

    Task<VisualizationProcessDetail?> GetProcessAsync(string id, CancellationToken cancellationToken);

    Task<PagedResult<VisualizationInstanceSummary>> GetInstancesAsync(
        PagedRequest request,
        long? definitionId,
        ApprovalInstanceStatus? status,
        CancellationToken cancellationToken);

    Task<VisualizationValidationResponse> ValidateAsync(ValidateVisualizationRequest request, CancellationToken cancellationToken);

    Task<SaveVisualizationProcessResponse> SaveProcessAsync(SaveVisualizationProcessRequest request, CancellationToken cancellationToken);

    Task<VisualizationPublishResponse> PublishAsync(PublishVisualizationRequest request, long publishedByUserId, CancellationToken cancellationToken);

    Task<VisualizationInstanceDetail?> GetInstanceAsync(string id, CancellationToken cancellationToken);

    Task<VisualizationMetricsResponse> GetMetricsAsync(VisualizationFilterRequest filter, CancellationToken cancellationToken);

    Task<PagedResult<AuditListItem>> GetAuditAsync(PagedRequest request, CancellationToken cancellationToken);
}
