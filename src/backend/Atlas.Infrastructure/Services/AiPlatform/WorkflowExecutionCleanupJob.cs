using Atlas.Application.AiPlatform.Repositories;
using Atlas.Core.Tenancy;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services.AiPlatform;

/// <summary>
/// PS-02: Hangfire 定时任务，清理超期工作流执行记录，避免数据库无限增长。
/// </summary>
public sealed class WorkflowExecutionCleanupJob
{
    /// <summary>默认保留执行记录的天数（90天）。</summary>
    public const int DefaultRetentionDays = 90;

    /// <summary>每次清理的最大条数，防止单次事务过大。</summary>
    private const int BatchSize = 500;

    private readonly IWorkflowExecutionRepository _executionRepo;
    private readonly IWorkflowNodeExecutionRepository _nodeExecutionRepo;
    private readonly ILogger<WorkflowExecutionCleanupJob> _logger;

    public WorkflowExecutionCleanupJob(
        IWorkflowExecutionRepository executionRepo,
        IWorkflowNodeExecutionRepository nodeExecutionRepo,
        ILogger<WorkflowExecutionCleanupJob> logger)
    {
        _executionRepo = executionRepo;
        _nodeExecutionRepo = nodeExecutionRepo;
        _logger = logger;
    }

    /// <summary>
    /// 清理 <paramref name="retentionDays"/> 天前已完成/取消/失败的执行记录。
    /// </summary>
    [JobDisplayName("WorkflowExecution Cleanup (retention={0}d)")]
    public async Task ExecuteAsync(int retentionDays = DefaultRetentionDays)
    {
        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
        _logger.LogInformation(
            "WorkflowExecutionCleanup started. Cutoff={Cutoff:yyyy-MM-dd} RetentionDays={Days}",
            cutoff, retentionDays);

        var deleted = await _executionRepo.DeleteCompletedBeforeAsync(cutoff, BatchSize, CancellationToken.None);
        _logger.LogInformation(
            "WorkflowExecutionCleanup completed. Deleted={DeletedCount} records",
            deleted);
    }
}
