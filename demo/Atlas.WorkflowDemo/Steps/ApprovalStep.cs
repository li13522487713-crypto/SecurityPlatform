using System.Text.Json;
using Atlas.WorkflowCore.Abstractions;
using Atlas.WorkflowCore.Models;
using Atlas.WorkflowCore.Primitives;
using Atlas.WorkflowDemo.Models;

namespace Atlas.WorkflowDemo.Steps;

/// <summary>
/// 审批步骤
/// </summary>
public class ApprovalStep : StepBody
{
    public override ExecutionResult Run(IStepExecutionContext context)
    {
        // 从JSON字符串反序列化数据
        var dataJson = context.Workflow.Data as string;
        var data = string.IsNullOrEmpty(dataJson) 
            ? new ApprovalWorkflowData() 
            : JsonSerializer.Deserialize<ApprovalWorkflowData>(dataJson)!;
        
        // 硬编码审批通过
        data.Approved = true;
        data.Approver = "审批人001";
        
        Console.WriteLine($"[步骤2] 审批通过");
        Console.WriteLine($"  审批人: {data.Approver}");
        Console.WriteLine($"  审批时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine();
        
        // 更新工作流数据
        context.Workflow.Data = JsonSerializer.Serialize(data);
        
        return ExecutionResult.Next();
    }
}
