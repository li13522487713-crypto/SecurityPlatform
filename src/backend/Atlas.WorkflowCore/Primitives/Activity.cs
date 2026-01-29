using Atlas.WorkflowCore.Abstractions;
using Atlas.WorkflowCore.Models;

namespace Atlas.WorkflowCore.Primitives;

/// <summary>
/// Activity 步骤 - 等待外部活动完成
/// </summary>
public class Activity : StepBody
{
    /// <summary>
    /// 活动名称
    /// </summary>
    public string ActivityName { get; set; } = string.Empty;

    /// <summary>
    /// 生效日期 - 活动在此日期之后才生效
    /// </summary>
    public DateTime EffectiveDate { get; set; }

    /// <summary>
    /// 活动参数
    /// </summary>
    public object? Parameters { get; set; }

    /// <summary>
    /// 活动结果
    /// </summary>
    public object? Result { get; set; }

    /// <summary>
    /// 活动令牌 - 用于标识和管理活动
    /// </summary>
    public string? Token { get; set; }

    /// <summary>
    /// 取消条件 - 当条件为 true 时，活动将被取消
    /// </summary>
    public bool CancelCondition { get; set; }

    /// <summary>
    /// 取消后是否继续执行
    /// </summary>
    public bool ProceedAfterCancel { get; set; }

    public override ExecutionResult Run(IStepExecutionContext context)
    {
        // 检查取消条件
        if (CancelCondition)
        {
            if (ProceedAfterCancel)
            {
                return ExecutionResult.Next();
            }
            return ExecutionResult.Outcome(null); // 取消并停止
        }

        // 如果事件尚未发布，等待活动
        if (!context.ExecutionPointer.EventPublished)
        {
            var effectiveDate = EffectiveDate != default ? EffectiveDate : DateTime.MinValue;
            var result = ExecutionResult.WaitForActivity(ActivityName, Parameters, effectiveDate);
            
            // 保存 token 到 persistence data 以便后续使用
            if (!string.IsNullOrEmpty(Token))
            {
                result.PersistenceData = Token;
            }
            
            return result;
        }

        // 活动已完成，获取结果
        Result = context.ExecutionPointer.EventData;
        Token = context.ExecutionPointer.PersistenceData?.ToString();
        
        return ExecutionResult.Next();
    }
}
