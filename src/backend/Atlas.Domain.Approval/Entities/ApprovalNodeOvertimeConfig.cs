using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.Approval.Entities;

/// <summary>
/// 审批节点超时配置（对应 AntFlow 的 BpmProcessNodeOvertime）
/// 配置存储在流程定义的节点配置中，此实体用于运行时查询
/// </summary>
public sealed class ApprovalNodeOvertimeConfig
{
    /// <summary>节点 ID</summary>
    public required string NodeId { get; init; }

    /// <summary>是否启用超时提醒</summary>
    public bool IsEnabled { get; init; }

    /// <summary>超时时间（小时）</summary>
    public int? TimeoutHours { get; init; }

    /// <summary>超时时间（分钟）</summary>
    public int? TimeoutMinutes { get; init; }

    /// <summary>提醒间隔（小时）</summary>
    public int? ReminderIntervalHours { get; init; }

    /// <summary>最大提醒次数</summary>
    public int? MaxReminderCount { get; init; }
}
