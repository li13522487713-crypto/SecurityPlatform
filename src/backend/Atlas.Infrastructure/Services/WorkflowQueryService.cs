using Atlas.Application.Workflow.Abstractions;
using Atlas.Application.Workflow.Models;
using Atlas.Core.Models;
using Atlas.WorkflowCore.Abstractions;
using Atlas.WorkflowCore.Abstractions.Persistence;
using AutoMapper;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services;

/// <summary>
/// 工作流查询服务实现
/// </summary>
public class WorkflowQueryService : IWorkflowQueryService
{
    private readonly IPersistenceProvider _persistenceProvider;
    private readonly IWorkflowRegistry _registry;
    private readonly IMapper _mapper;
    private readonly ILogger<WorkflowQueryService> _logger;

    public WorkflowQueryService(
        IPersistenceProvider persistenceProvider,
        IWorkflowRegistry registry,
        IMapper mapper,
        ILogger<WorkflowQueryService> logger)
    {
        _persistenceProvider = persistenceProvider;
        _registry = registry;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<WorkflowInstanceResponse?> GetWorkflowInstanceAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        var instance = await _persistenceProvider.GetWorkflowAsync(instanceId, cancellationToken);
        if (instance == null)
        {
            _logger.LogWarning("工作流实例不存在: {InstanceId}", instanceId);
            return null;
        }

        return _mapper.Map<WorkflowInstanceResponse>(instance);
    }

    public async Task<PagedResult<WorkflowInstanceListItem>> GetWorkflowInstancesAsync(PagedRequest request, CancellationToken cancellationToken = default)
    {
        // 获取可运行的实例（临时实现，实际应该支持分页和过滤）
        var instances = await _persistenceProvider.GetRunnableInstancesAsync(DateTime.UtcNow, cancellationToken);
        var items = _mapper.Map<IEnumerable<WorkflowInstanceListItem>>(instances).ToList();

        // 简单的分页处理
        var pagedItems = items
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        return new PagedResult<WorkflowInstanceListItem>(
            pagedItems,
            items.Count,
            request.PageIndex,
            request.PageSize);
    }

    public Task<IEnumerable<WorkflowDefinitionResponse>> GetAllDefinitionsAsync(CancellationToken cancellationToken = default)
    {
        var definitions = _registry.GetAllDefinitions();
        var response = _mapper.Map<IEnumerable<WorkflowDefinitionResponse>>(definitions);
        return Task.FromResult(response);
    }

    public Task<WorkflowDefinitionResponse?> GetDefinitionAsync(string workflowId, int? version = null, CancellationToken cancellationToken = default)
    {
        var definition = _registry.GetDefinition(workflowId, version);
        if (definition == null)
        {
            _logger.LogWarning("工作流定义不存在: {WorkflowId} v{Version}", workflowId, version);
            return Task.FromResult<WorkflowDefinitionResponse?>(null);
        }

        var response = _mapper.Map<WorkflowDefinitionResponse>(definition);
        return Task.FromResult<WorkflowDefinitionResponse?>(response);
    }

    public async Task<IEnumerable<ExecutionPointerResponse>> GetExecutionPointersAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        var instance = await _persistenceProvider.GetWorkflowAsync(instanceId, cancellationToken);
        if (instance == null)
        {
            _logger.LogWarning("工作流实例不存在: {InstanceId}", instanceId);
            return Enumerable.Empty<ExecutionPointerResponse>();
        }

        var definition = _registry.GetDefinition(instance.WorkflowDefinitionId, instance.Version);
        
        var pointers = instance.ExecutionPointers.Select(pointer =>
        {
            var step = definition?.Steps.FirstOrDefault(s => s.Id == pointer.StepId);
            var stepName = step?.Name ?? $"步骤{pointer.StepId}";

            // 确定状态
            string status;
            if (!pointer.EndTime.HasValue)
            {
                if (pointer.SleepUntil.HasValue)
                    status = "Sleeping";
                else if (!string.IsNullOrEmpty(pointer.EventName))
                    status = "WaitingForEvent";
                else if (pointer.Active)
                    status = "Running";
                else
                    status = "Pending";
            }
            else if (!string.IsNullOrEmpty(pointer.EventName) && pointer.EventPublished == false)
            {
                status = "WaitingForEvent";
            }
            else
            {
                status = "Complete";
            }

            return new ExecutionPointerResponse
            {
                Id = pointer.Id,
                StepId = pointer.StepId,
                StepName = stepName,
                Active = pointer.Active,
                StartTime = pointer.StartTime,
                EndTime = pointer.EndTime,
                Status = status,
                RetryCount = pointer.RetryCount,
                ErrorMessage = null, // ExecutionPointer 没有直接存储错误消息
                SleepUntil = pointer.SleepUntil,
                EventName = pointer.EventName,
                EventKey = pointer.EventKey
            };
        }).ToList();

        return pointers;
    }
}
