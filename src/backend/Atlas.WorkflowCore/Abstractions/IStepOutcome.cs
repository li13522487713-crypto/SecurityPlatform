using Atlas.WorkflowCore.Models;

namespace Atlas.WorkflowCore.Abstractions;

/// <summary>
/// 步骤结果接口
/// </summary>
public interface IStepOutcome
{
    /// <summary>
    /// 下一个步骤ID
    /// </summary>
    int NextStep { get; set; }
    
    /// <summary>
    /// 外部下一个步骤ID
    /// </summary>
    string? ExternalNextStepId { get; set; }
    
    /// <summary>
    /// 结果标签
    /// </summary>
    string? Label { get; set; }

    /// <summary>
    /// 匹配数据对象
    /// </summary>
    bool Matches(object? data);

    /// <summary>
    /// 匹配执行结果和数据对象
    /// </summary>
    bool Matches(ExecutionResult executionResult, object? data);
}
