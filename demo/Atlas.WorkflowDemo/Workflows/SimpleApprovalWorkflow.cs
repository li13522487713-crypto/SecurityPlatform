using Atlas.WorkflowCore.Abstractions;
using Atlas.WorkflowDemo.Models;
using Atlas.WorkflowDemo.Steps;

namespace Atlas.WorkflowDemo.Workflows;

/// <summary>
/// 简单审批工作流定义
/// </summary>
public class SimpleApprovalWorkflow : IWorkflow<ApprovalWorkflowData>
{
    public string Id => "simple-approval";
    public int Version => 1;

    public void Build(IWorkflowBuilder<ApprovalWorkflowData> builder)
    {
        builder
            .StartWith<SubmitApplicationStep>()
            .Then<ApprovalStep>()
            .Then<CompleteStep>();
    }
}
