using Atlas.Application.Workflow.Abstractions;
using Atlas.Application.Workflow.Models;
using Atlas.WorkflowCore.Abstractions;
using Atlas.WorkflowCore.DSL.Interface;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services;

/// <summary>
/// 工作流命令服务实现
/// </summary>
public class WorkflowCommandService : IWorkflowCommandService
{
    private readonly IWorkflowHost _workflowHost;
    private readonly IWorkflowRegistry _registry;
    private readonly IDefinitionLoader _definitionLoader;
    private readonly IValidator<StartWorkflowRequest> _startWorkflowValidator;
    private readonly IValidator<PublishEventRequest> _publishEventValidator;
    private readonly ILogger<WorkflowCommandService> _logger;

    public WorkflowCommandService(
        IWorkflowHost workflowHost,
        IWorkflowRegistry registry,
        IDefinitionLoader definitionLoader,
        IValidator<StartWorkflowRequest> startWorkflowValidator,
        IValidator<PublishEventRequest> publishEventValidator,
        ILogger<WorkflowCommandService> logger)
    {
        _workflowHost = workflowHost;
        _registry = registry;
        _definitionLoader = definitionLoader;
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
            null,
            cancellationToken);

        _logger.LogInformation("外部事件已发布: {EventName}#{EventKey}", request.EventName, request.EventKey);
    }

    public Task RegisterWorkflowFromJsonAsync(RegisterWorkflowDefinitionRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.WorkflowId))
        {
            throw new ArgumentException("工作流ID不能为空", nameof(request.WorkflowId));
        }

        if (string.IsNullOrWhiteSpace(request.DefinitionJson))
        {
            throw new ArgumentException("工作流定义不能为空", nameof(request.DefinitionJson));
        }

        // 使用 DSL DefinitionLoader 加载工作流定义
        var definition = _definitionLoader.LoadDefinitionFromJson(request.DefinitionJson);
        
        // 覆盖 ID 和 Version
        definition.Id = request.WorkflowId;
        definition.Version = request.Version;

        // 注册到工作流注册表
        _registry.RegisterWorkflow(definition);

        _logger.LogInformation("动态工作流已注册: {WorkflowId} v{Version}", request.WorkflowId, request.Version);
        
        return Task.CompletedTask;
    }
}
