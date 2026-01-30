using Atlas.Application.Visualization.Models;
using Atlas.Core.Models;

namespace Atlas.Application.Visualization.Abstractions;

/// <summary>
/// 可视化中心查询与操作契约（骨架版，后续可替换为真实实现）
/// </summary>
public interface IVisualizationQueryService
{
    Task<VisualizationOverviewResponse> GetOverviewAsync(VisualizationFilterRequest filter, CancellationToken cancellationToken);

    Task<PagedResult<VisualizationProcessSummary>> GetProcessesAsync(PagedRequest request, CancellationToken cancellationToken);

    Task<PagedResult<VisualizationInstanceSummary>> GetInstancesAsync(PagedRequest request, CancellationToken cancellationToken);

    Task<VisualizationValidationResponse> ValidateAsync(ValidateVisualizationRequest request, CancellationToken cancellationToken);

    Task<VisualizationPublishResponse> PublishAsync(PublishVisualizationRequest request, CancellationToken cancellationToken);
}
