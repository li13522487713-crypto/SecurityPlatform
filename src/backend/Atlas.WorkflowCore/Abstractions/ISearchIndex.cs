using Atlas.WorkflowCore.Models;

namespace Atlas.WorkflowCore.Abstractions;

/// <summary>
/// 搜索索引接口
/// </summary>
public interface ISearchIndex
{
    /// <summary>
    /// 索引工作流实例
    /// </summary>
    /// <param name="workflow">工作流实例</param>
    Task IndexWorkflow(WorkflowInstance workflow);
}
