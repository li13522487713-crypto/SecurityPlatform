using Atlas.Application.Visualization.Abstractions;
using Atlas.Application.Visualization.Models;
using Atlas.Core.Models;

namespace Atlas.Infrastructure.Services.Visualization;

/// <summary>
/// 可视化中心骨架实现：返回示例数据，便于前后端联调。
/// </summary>
public sealed class VisualizationQueryService : IVisualizationQueryService
{
    private static readonly string[] DefaultRisks = { "节点超时偏高", "部分流程版本未发布", "告警升级链路缺失回退" };

    public Task<VisualizationOverviewResponse> GetOverviewAsync(VisualizationFilterRequest filter, CancellationToken cancellationToken)
    {
        var overview = new VisualizationOverviewResponse(
            TotalProcesses: 12,
            RunningInstances: 128,
            BlockedNodes: 5,
            AlertsToday: 17,
            RiskHints: DefaultRisks);

        return Task.FromResult(overview);
    }

    public Task<PagedResult<VisualizationProcessSummary>> GetProcessesAsync(PagedRequest request, CancellationToken cancellationToken)
    {
        var items = Enumerable.Range(1, request.PageSize).Select(i => new VisualizationProcessSummary
        {
            Id = $"flow-{request.PageIndex}-{i}",
            Name = $"示例流程 {i}",
            Version = 1,
            Status = i % 2 == 0 ? "Published" : "Draft",
            PublishedAt = DateTimeOffset.UtcNow.AddDays(-i)
        }).ToList();

        var result = new PagedResult<VisualizationProcessSummary>(
            items,
            42,
            request.PageIndex,
            request.PageSize);

        return Task.FromResult(result);
    }

    public Task<PagedResult<VisualizationInstanceSummary>> GetInstancesAsync(PagedRequest request, CancellationToken cancellationToken)
    {
        var items = Enumerable.Range(1, request.PageSize).Select(i => new VisualizationInstanceSummary
        {
            Id = $"inst-{request.PageIndex}-{i}",
            FlowName = $"示例流程 {i}",
            Status = i % 3 == 0 ? "Blocked" : "Running",
            CurrentNode = "审批节点",
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-30 * i),
            DurationMinutes = 30 * i
        }).ToList();

        var result = new PagedResult<VisualizationInstanceSummary>(
            items,
            128,
            request.PageIndex,
            request.PageSize);

        return Task.FromResult(result);
    }

    public Task<VisualizationValidationResponse> ValidateAsync(ValidateVisualizationRequest request, CancellationToken cancellationToken)
    {
        // 骨架版：简单校验 JSON 是否非空
        var passed = !string.IsNullOrWhiteSpace(request.DefinitionJson);
        var errors = passed ? Array.Empty<string>() : new[] { "定义内容为空" };
        return Task.FromResult(new VisualizationValidationResponse(passed, errors));
    }

    public Task<VisualizationPublishResponse> PublishAsync(PublishVisualizationRequest request, CancellationToken cancellationToken)
    {
        var response = new VisualizationPublishResponse(request.ProcessId, request.Version, "Published");
        return Task.FromResult(response);
    }
}
