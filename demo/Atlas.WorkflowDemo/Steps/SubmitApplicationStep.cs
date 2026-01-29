using System.Text.Json;
using Atlas.WorkflowCore.Abstractions;
using Atlas.WorkflowCore.Models;
using Atlas.WorkflowCore.Primitives;
using Atlas.WorkflowDemo.Models;

namespace Atlas.WorkflowDemo.Steps;

/// <summary>
/// 提交申请步骤
/// </summary>
public class SubmitApplicationStep : StepBody
{
    public override ExecutionResult Run(IStepExecutionContext context)
    {
        // 从JSON字符串反序列化数据
        var dataJson = context.Workflow.Data as string;
        var data = string.IsNullOrEmpty(dataJson) 
            ? new ApprovalWorkflowData() 
            : JsonSerializer.Deserialize<ApprovalWorkflowData>(dataJson)!;
        
        Console.WriteLine();
        Console.WriteLine("=== 审批流程开始 ===");
        Console.WriteLine($"[步骤1] 提交申请: {data.ApplicationTitle}");
        Console.WriteLine($"  申请人: {data.Applicant}");
        Console.WriteLine($"  申请时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine();
        
        return ExecutionResult.Next();
    }
}
