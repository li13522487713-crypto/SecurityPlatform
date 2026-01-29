using Atlas.WorkflowCore.Models;
using Atlas.WorkflowCore.Models.Search;

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

    /// <summary>
    /// 搜索工作流
    /// </summary>
    /// <param name="filter">搜索过滤器</param>
    /// <param name="skip">跳过的数量</param>
    /// <param name="take">获取的数量</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>分页的搜索结果</returns>
    Task<Page<WorkflowSearchResult>> Search(SearchFilter filter, int skip, int take, CancellationToken cancellationToken = default);
}
