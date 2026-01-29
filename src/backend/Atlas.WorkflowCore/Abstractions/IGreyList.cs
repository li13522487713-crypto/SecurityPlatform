namespace Atlas.WorkflowCore.Abstractions;

/// <summary>
/// 灰名单管理接口 - 防止重复处理
/// </summary>
public interface IGreyList
{
    /// <summary>
    /// 添加项到灰名单
    /// </summary>
    /// <param name="id">项ID</param>
    void Add(string id);

    /// <summary>
    /// 从灰名单移除项
    /// </summary>
    /// <param name="id">项ID</param>
    void Remove(string id);

    /// <summary>
    /// 检查项是否在灰名单中
    /// </summary>
    /// <param name="id">项ID</param>
    /// <returns>如果在灰名单中返回true</returns>
    bool Contains(string id);
}
