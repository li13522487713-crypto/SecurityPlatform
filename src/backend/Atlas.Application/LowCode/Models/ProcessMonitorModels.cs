namespace Atlas.Application.LowCode.Models;

/// <summary>
/// 流程监控仪表盘数据
/// </summary>
public sealed record ProcessMonitorDashboard(
    int ActiveInstances,
    int CompletedToday,
    int RejectedToday,
    int TotalDefinitions,
    int PendingTasks,
    int OverdueTasks,
    double AvgCompletionMinutes,
    IReadOnlyList<ProcessNodeBottleneck> Bottlenecks,
    IReadOnlyList<ProcessDailyStats> DailyStats);

/// <summary>
/// 节点耗时瓶颈
/// </summary>
public sealed record ProcessNodeBottleneck(
    string NodeId,
    string NodeName,
    string FlowName,
    double AvgDurationMinutes,
    int PendingCount);

/// <summary>
/// 每日统计
/// </summary>
public sealed record ProcessDailyStats(
    string Date,
    int Started,
    int Completed,
    int Rejected);

/// <summary>
/// 流程实例追踪（带节点高亮状态）
/// </summary>
public sealed record ProcessInstanceTrace(
    string InstanceId,
    string FlowName,
    string Status,
    string InitiatorUserId,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    IReadOnlyList<ProcessNodeTrace> Nodes);

/// <summary>
/// 节点追踪信息
/// </summary>
public sealed record ProcessNodeTrace(
    string NodeId,
    string NodeName,
    string NodeType,
    string Status,
    DateTimeOffset? StartedAt,
    DateTimeOffset? EndedAt,
    double? DurationMinutes,
    string? AssigneeName,
    string? Comment);
