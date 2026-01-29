using Atlas.Application.Workflow.Abstractions;
using Atlas.Application.Workflow.Models;
using Atlas.WorkflowCore.Abstractions;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services;

/// <summary>
/// 工作流命令服务实现
/// </summary>
public class WorkflowCommandService : IWorkflowCommandService
{
    private readonly IWorkflowHost _workflowHost;
    private readonly IValidator<StartWorkflowRequest> _startWorkflowValidator;
    private readonly IValidator<PublishEventRequest> _publishEventValidator;
    private readonly ILogger<WorkflowCommandService> _logger;

    public WorkflowCommandService(
        IWorkflowHost workflowHost,
        IValidator<StartWorkflowRequest> startWorkflowValidator,
        IValidator<PublishEventRequest> publishEventValidator,
        ILogger<WorkflowCommandService> logger)
    {
        _workflowHost = workflowHost;
        _startWorkflowValidator = startWorkflowValidator;
        _publishEventValidator = publishEventValidator;
        _logger = logger;
    }

    public async Task<string> StartWorkflowAsync(StartWorkflowRequest request, CancellationToken cancellationToken = default)
    {
        await _startWorkflowValidator.ValidateAndThrowAsync(request, cancellationToken);

        var instanceId = await _workflowHost.StartWorkflowAsync(
            request.WorkflowId,
            request.Version,
            request.Data,
            request.Reference,
            cancellationToken);

        _logger.LogInformation("工作流实例已启动: {InstanceId}", instanceId);
        return instanceId;
    }

    public async Task<bool> SuspendWorkflowAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        var result = await _workflowHost.SuspendWorkflowAsync(instanceId, cancellationToken);
        if (result)
        {
            _logger.LogInformation("工作流实例已挂起: {InstanceId}", instanceId);
        }
        return result;
    }

    public async Task<bool> ResumeWorkflowAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        var result = await _workflowHost.ResumeWorkflowAsync(instanceId, cancellationToken);
        if (result)
        {
            _logger.LogInformation("工作流实例已恢复: {InstanceId}", instanceId);
        }
        return result;
    }

    public async Task<bool> TerminateWorkflowAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        var result = await _workflowHost.TerminateWorkflowAsync(instanceId, cancellationToken);
        if (result)
        {
            _logger.LogInformation("工作流实例已终止: {InstanceId}", instanceId);
        }
        return result;
    }

    public async Task PublishEventAsync(PublishEventRequest request, CancellationToken cancellationToken = default)
    {
        await _publishEventValidator.ValidateAndThrowAsync(request, cancellationToken);

        await _workflowHost.PublishEventAsync(
            request.EventName,
            request.EventKey,
            request.EventData,
            cancellationToken);

        _logger.LogInformation("外部事件已发布: {EventName}#{EventKey}", request.EventName, request.EventKey);
    }
}
