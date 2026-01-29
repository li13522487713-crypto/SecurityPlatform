using Atlas.WorkflowCore.Abstractions;
using Atlas.WorkflowCore.Models;

namespace Atlas.WorkflowCore.Services.DefaultProviders;

/// <summary>
/// 空搜索索引实现 - 不执行任何索引操作
/// </summary>
public class NullSearchIndex : ISearchIndex
{
    public Task IndexWorkflow(WorkflowInstance workflow)
    {
        // 不执行任何操作
        return Task.CompletedTask;
    }
}
