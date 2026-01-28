namespace Atlas.Domain.Approval.Enums;

/// <summary>
/// 回调状态
/// </summary>
public enum CallbackStatus
{
    /// <summary>待发送</summary>
    Pending = 0,

    /// <summary>发送中</summary>
    Sending = 1,

    /// <summary>成功</summary>
    Success = 2,

    /// <summary>失败</summary>
    Failed = 3,

    /// <summary>已取消</summary>
    Cancelled = 4
}
