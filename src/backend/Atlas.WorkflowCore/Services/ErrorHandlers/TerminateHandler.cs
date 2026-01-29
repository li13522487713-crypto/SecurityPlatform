using System;
using System.Collections.Generic;
using Atlas.WorkflowCore.Abstractions;
using Atlas.WorkflowCore.Models;
using Atlas.WorkflowCore.Models.LifeCycleEvents;
using Microsoft.Extensions.Logging;

namespace Atlas.WorkflowCore.Services.ErrorHandlers;

/// <summary>
/// 终止错误处理器
/// </summary>
public class TerminateHandler : IWorkflowErrorHandler
{
    private readonly ILifeCycleEventPublisher _eventPublisher;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<TerminateHandler> _logger;

    public WorkflowErrorHandling Type => WorkflowErrorHandling.Terminate;

    public TerminateHandler(
        ILifeCycleEventPublisher eventPublisher,
        IDateTimeProvider dateTimeProvider,
        ILogger<TerminateHandler> logger)
    {
        _eventPublisher = eventPublisher;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public void Handle(
        WorkflowInstance workflow,
        WorkflowDefinition def,
        ExecutionPointer pointer,
        WorkflowStep step,
        Exception exception,
        Queue<ExecutionPointer> bubbleUpQueue)
    {
        workflow.Status = WorkflowStatus.Terminated;
        workflow.CompleteTime = _dateTimeProvider.UtcNow;

        _eventPublisher.PublishNotification(new WorkflowTerminated
        {
            EventTimeUtc = _dateTimeProvider.UtcNow,
            Reference = workflow.Reference,
            WorkflowInstanceId = workflow.Id,
            WorkflowDefinitionId = workflow.WorkflowDefinitionId,
            Version = workflow.Version
        });

        _logger.LogError(exception,
            "步骤 {StepName} 执行失败，工作流已终止: {WorkflowId}",
            step.Name, workflow.Id);
    }
}
