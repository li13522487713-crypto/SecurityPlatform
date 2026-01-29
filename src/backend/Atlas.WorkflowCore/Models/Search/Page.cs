namespace Atlas.WorkflowCore.Models.Search;

/// <summary>
/// 分页结果
/// </summary>
/// <typeparam name="T">数据类型</typeparam>
public class Page<T>
{
    /// <summary>
    /// 数据项
    /// </summary>
    public List<T> Data { get; set; } = new();

    /// <summary>
    /// 总数
    /// </summary>
    public long Total { get; set; }

    /// <summary>
    /// 跳过的数量
    /// </summary>
    public int Skip { get; set; }

    /// <summary>
    /// 获取的数量
    /// </summary>
    public int Take { get; set; }
}
