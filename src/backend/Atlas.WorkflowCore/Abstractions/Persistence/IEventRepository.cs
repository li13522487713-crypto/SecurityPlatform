using Atlas.WorkflowCore.Models;

namespace Atlas.WorkflowCore.Abstractions.Persistence;

/// <summary>
/// 事件仓储接口
/// </summary>
public interface IEventRepository
{
    /// <summary>
    /// 创建事件
    /// </summary>
    /// <param name="newEvent">事件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>事件ID</returns>
    Task<string> CreateEvent(Event newEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取事件
    /// </summary>
    /// <param name="id">事件ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>事件</returns>
    Task<Event> GetEvent(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取可运行的事件ID列表
    /// </summary>
    /// <param name="asAt">时间点</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>事件ID列表</returns>
    Task<IEnumerable<string>> GetRunnableEvents(DateTime asAt, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取事件ID列表
    /// </summary>
    /// <param name="eventName">事件名称</param>
    /// <param name="eventKey">事件键</param>
    /// <param name="asOf">时间点</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>事件ID列表</returns>
    Task<IEnumerable<string>> GetEvents(string eventName, string eventKey, DateTime asOf, CancellationToken cancellationToken = default);

    /// <summary>
    /// 标记事件已处理
    /// </summary>
    /// <param name="id">事件ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task MarkEventProcessed(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 标记事件未处理
    /// </summary>
    /// <param name="id">事件ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task MarkEventUnprocessed(string id, CancellationToken cancellationToken = default);
}
