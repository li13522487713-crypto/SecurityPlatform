using System;
using System.Collections.Generic;
using Atlas.WorkflowCore.Abstractions;
using Atlas.WorkflowCore.Models;
using Microsoft.Extensions.Logging;

namespace Atlas.WorkflowCore.Services.ErrorHandlers;

/// <summary>
/// 暂停错误处理器
/// </summary>
public class SuspendHandler : IWorkflowErrorHandler
{
    private readonly ILogger<SuspendHandler> _logger;

    public WorkflowErrorHandling Type => WorkflowErrorHandling.Suspend;

    public SuspendHandler(ILogger<SuspendHandler> logger)
    {
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
        pointer.Status = PointerStatus.Failed;
        pointer.Active = false;
        pointer.EndTime = DateTime.UtcNow;

        workflow.Status = WorkflowStatus.Suspended;

        _logger.LogError(exception,
            "步骤 {StepName} 执行失败，工作流已暂停: {WorkflowId}",
            step.Name, workflow.Id);
    }
}
