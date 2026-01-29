using Atlas.WorkflowCore.Abstractions;
using Atlas.WorkflowCore.Models;
using Atlas.WorkflowCore.Models.Search;

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

    public Task<Page<WorkflowSearchResult>> Search(SearchFilter filter, int skip, int take, CancellationToken cancellationToken = default)
    {
        // 返回空结果
        return Task.FromResult(new Page<WorkflowSearchResult>
        {
            Data = new List<WorkflowSearchResult>(),
            Total = 0,
            Skip = skip,
            Take = take
        });
    }
}
