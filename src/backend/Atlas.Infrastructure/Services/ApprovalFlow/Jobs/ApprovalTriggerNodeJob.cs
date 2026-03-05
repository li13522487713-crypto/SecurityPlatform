using Atlas.Application.Approval.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Approval.Enums;
using Atlas.Infrastructure.Services.ApprovalFlow;
using Microsoft.Extensions.Logging;
using SqlSugar;

namespace Atlas.Infrastructure.Services.ApprovalFlow.Jobs;

/// <summary>
/// 触发器节点任务（后台定时扫描到期的 Trigger 节点并执行）
/// </summary>
public sealed class ApprovalTriggerNodeJob
{
    private readonly ISqlSugarClient _db;
    private readonly IApprovalInstanceRepository _instanceRepository;
    private readonly IApprovalFlowRepository _flowRepository;
    private readonly IApprovalNodeExecutionRepository _nodeExecutionRepository;
    private readonly FlowEngine _flowEngine;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ApprovalTriggerNodeJob> _logger;

    public ApprovalTriggerNodeJob(
        ISqlSugarClient db,
        IApprovalInstanceRepository instanceRepository,
        IApprovalFlowRepository flowRepository,
        IApprovalNodeExecutionRepository nodeExecutionRepository,
        FlowEngine flowEngine,
        TimeProvider timeProvider,
        ILogger<ApprovalTriggerNodeJob> logger)
    {
        _db = db;
        _instanceRepository = instanceRepository;
        _flowRepository = flowRepository;
        _nodeExecutionRepository = nodeExecutionRepository;
        _flowEngine = flowEngine;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        // 跨租户扫描所有到期任务，再按租户分批加载关联数据，避免租户数据串读
        var dueJobs = await _db.Queryable<ApprovalTriggerJob>()
            .Where(j => j.Status == 0 && j.ScheduledAt <= now)
            .ToListAsync(cancellationToken);

        if (dueJobs.Count == 0)
        {
            return;
        }

        foreach (var tenantJobsGroup in dueJobs.GroupBy(x => x.TenantIdValue))
        {
            try
            {
                var tenantId = new Atlas.Core.Tenancy.TenantId(tenantJobsGroup.Key);
                var tenantJobs = tenantJobsGroup.ToList();
                var instanceIds = tenantJobs.Select(j => j.InstanceId).Distinct().ToList();
                var instancesById = (await _instanceRepository.QueryByIdsAsync(tenantId, instanceIds, cancellationToken))
                    .ToDictionary(i => i.Id);

                var definitionIds = instancesById.Values.Select(i => i.DefinitionId).Distinct().ToList();
                var flowDefsById = (await _flowRepository.QueryByIdsAsync(tenantId, definitionIds, cancellationToken))
                    .ToDictionary(f => f.Id);

                foreach (var job in tenantJobs)
                {
                    try
                    {
                        if (!instancesById.TryGetValue(job.InstanceId, out var instance)
                            || instance.Status != ApprovalInstanceStatus.Running)
                        {
                            job.MarkCancelled(now);
                            await _db.Updateable(job)
                                .Where(x => x.Id == job.Id && x.TenantIdValue == job.TenantIdValue)
                                .ExecuteCommandAsync(cancellationToken);
                            continue;
                        }

                        if (!flowDefsById.TryGetValue(instance.DefinitionId, out var flowDef))
                        {
                            continue;
                        }

                        var flowDefinition = FlowDefinitionParser.Parse(flowDef.DefinitionJson);

                        // 标记触发器节点执行完成
                        var nodeExecution = await _nodeExecutionRepository.GetByInstanceAndNodeAsync(
                            job.TenantId, job.InstanceId, job.NodeId, cancellationToken);
                        if (nodeExecution != null)
                        {
                            nodeExecution.MarkCompleted(now);
                            await _nodeExecutionRepository.UpdateAsync(nodeExecution, cancellationToken);
                        }

                        // 推进流程到下一个节点
                        await _flowEngine.AdvanceFlowAsync(job.TenantId, instance, flowDefinition, job.NodeId, cancellationToken);
                        await _instanceRepository.UpdateAsync(instance, cancellationToken);

                        job.MarkExecuted(now);
                        await _db.Updateable(job)
                            .Where(x => x.Id == job.Id && x.TenantIdValue == job.TenantIdValue)
                            .ExecuteCommandAsync(cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "执行触发器节点任务失败: {JobId}", job.Id);
                        job.MarkFailed(now, ex.Message);
                        await _db.Updateable(job)
                            .Where(x => x.Id == job.Id && x.TenantIdValue == job.TenantIdValue)
                            .ExecuteCommandAsync(cancellationToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载租户 {TenantId} 的实例或流程定义失败，跳过该租户批次", tenantJobsGroup.Key);
            }
        }
    }
}
