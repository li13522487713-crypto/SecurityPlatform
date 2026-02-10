using Atlas.Application.Approval.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Domain.Approval.Entities;
using Atlas.Infrastructure.Services.ApprovalFlow;
using Microsoft.Extensions.Logging;
using SqlSugar;

namespace Atlas.Infrastructure.Services.ApprovalFlow.Jobs;

/// <summary>
/// 触发器节点任务
/// </summary>
public sealed class ApprovalTriggerNodeJob
{
    private readonly ISqlSugarClient _db;
    private readonly IApprovalInstanceRepository _instanceRepository;
    private readonly IApprovalFlowRepository _flowRepository;
    private readonly FlowEngine _flowEngine;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ApprovalTriggerNodeJob> _logger;

    public ApprovalTriggerNodeJob(
        ISqlSugarClient db,
        IApprovalInstanceRepository instanceRepository,
        IApprovalFlowRepository flowRepository,
        FlowEngine flowEngine,
        TimeProvider timeProvider,
        ILogger<ApprovalTriggerNodeJob> logger)
    {
        _db = db;
        _instanceRepository = instanceRepository;
        _flowRepository = flowRepository;
        _flowEngine = flowEngine;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        // 查询到期任务
        var jobs = await _db.Queryable<ApprovalTriggerJob>()
            .Where(j => j.Status == 0 && j.ScheduledAt <= now)
            .ToListAsync(cancellationToken);

        foreach (var job in jobs)
        {
            try
            {
                var instance = await _instanceRepository.GetByIdAsync(job.TenantId, job.InstanceId, cancellationToken);
                if (instance == null)
                {
                    job.MarkCancelled(now);
                    await _db.Updateable(job).ExecuteCommandAsync(cancellationToken);
                    continue;
                }

                // 执行触发器逻辑（调用外部服务等）
                // ...

                var flowDef = await _flowRepository.GetByIdAsync(job.TenantId, instance.DefinitionId, cancellationToken);
                if (flowDef == null) continue;

                var flowDefinition = FlowDefinitionParser.Parse(flowDef.DefinitionJson);

                // 推进流程
                await _flowEngine.AdvanceFlowAsync(job.TenantId, instance, flowDefinition, job.NodeId, cancellationToken);
                await _instanceRepository.UpdateAsync(instance, cancellationToken);

                job.MarkExecuted(now);
                await _db.Updateable(job).ExecuteCommandAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "执行触发器节点任务失败: {JobId}", job.Id);
                job.MarkFailed(now, ex.Message);
                await _db.Updateable(job).ExecuteCommandAsync(cancellationToken);
            }
        }
    }
}
