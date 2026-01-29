using Atlas.WorkflowCore.Models;

namespace Atlas.WorkflowCore.Abstractions.Persistence;

/// <summary>
/// 事件订阅仓储接口
/// </summary>
public interface ISubscriptionRepository
{
    /// <summary>
    /// 创建事件订阅
    /// </summary>
    /// <param name="subscription">事件订阅</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>订阅ID</returns>
    Task<string> CreateEventSubscription(EventSubscription subscription, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取订阅列表
    /// </summary>
    /// <param name="eventName">事件名称</param>
    /// <param name="eventKey">事件键</param>
    /// <param name="asOf">时间点</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>订阅列表</returns>
    Task<IEnumerable<EventSubscription>> GetSubscriptions(string eventName, string eventKey, DateTime asOf, CancellationToken cancellationToken = default);

    /// <summary>
    /// 终止订阅
    /// </summary>
    /// <param name="eventSubscriptionId">订阅ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task TerminateSubscription(string eventSubscriptionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取订阅
    /// </summary>
    /// <param name="eventSubscriptionId">订阅ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>订阅</returns>
    Task<EventSubscription> GetSubscription(string eventSubscriptionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取第一个开放的订阅
    /// </summary>
    /// <param name="eventName">事件名称</param>
    /// <param name="eventKey">事件键</param>
    /// <param name="asOf">时间点</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>订阅</returns>
    Task<EventSubscription> GetFirstOpenSubscription(string eventName, string eventKey, DateTime asOf, CancellationToken cancellationToken = default);

    /// <summary>
    /// 设置订阅令牌
    /// </summary>
    /// <param name="eventSubscriptionId">订阅ID</param>
    /// <param name="token">令牌</param>
    /// <param name="workerId">工作节点ID</param>
    /// <param name="expiry">过期时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    Task<bool> SetSubscriptionToken(string eventSubscriptionId, string token, string workerId, DateTime expiry, CancellationToken cancellationToken = default);

    /// <summary>
    /// 清除订阅令牌
    /// </summary>
    /// <param name="eventSubscriptionId">订阅ID</param>
    /// <param name="token">令牌</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task ClearSubscriptionToken(string eventSubscriptionId, string token, CancellationToken cancellationToken = default);
}
