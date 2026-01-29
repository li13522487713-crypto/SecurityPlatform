using System.Text.Json;
using Atlas.WorkflowCore.Abstractions;
using Atlas.WorkflowCore.Models;
using Atlas.WorkflowCore.Primitives;
using Atlas.WorkflowDemo.Models;

namespace Atlas.WorkflowDemo.Steps;

/// <summary>
/// 完成步骤
/// </summary>
public class CompleteStep : StepBody
{
    public override ExecutionResult Run(IStepExecutionContext context)
    {
        // 从JSON字符串反序列化数据
        var dataJson = context.Workflow.Data as string;
        var data = string.IsNullOrEmpty(dataJson) 
            ? new ApprovalWorkflowData() 
            : JsonSerializer.Deserialize<ApprovalWorkflowData>(dataJson)!;
        
        Console.WriteLine($"[步骤3] 流程完成");
        Console.WriteLine($"  最终结果: {(data.Approved ? "✓ 已批准" : "✗ 已拒绝")}");
        Console.WriteLine($"  完成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine("=== 审批流程结束 ===");
        Console.WriteLine();
        
        return ExecutionResult.Next();
    }
}
