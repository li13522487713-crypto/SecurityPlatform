namespace Atlas.Domain.Approval.Enums;

/// <summary>
/// BPMN 2.0 扩展节点类型
/// </summary>
public enum BpmnNodeType
{
    // ─── 事件 ───
    /// <summary>开始事件</summary>
    StartEvent = 0,
    /// <summary>结束事件</summary>
    EndEvent = 1,
    /// <summary>定时器事件</summary>
    TimerEvent = 2,
    /// <summary>信号事件</summary>
    SignalEvent = 3,
    /// <summary>消息事件</summary>
    MessageEvent = 4,
    /// <summary>错误事件</summary>
    ErrorEvent = 5,

    // ─── 任务 ───
    /// <summary>用户任务（人工审批）</summary>
    UserTask = 10,
    /// <summary>服务任务（自动 API 调用）</summary>
    ServiceTask = 11,
    /// <summary>脚本任务（JS/C# 表达式）</summary>
    ScriptTask = 12,
    /// <summary>发送任务（发送通知/消息）</summary>
    SendTask = 13,
    /// <summary>接收任务（等待外部消息）</summary>
    ReceiveTask = 14,

    // ─── 网关 ───
    /// <summary>排他网关（条件分支，只走一条）</summary>
    ExclusiveGateway = 20,
    /// <summary>并行网关（并行分支，全部执行）</summary>
    ParallelGateway = 21,
    /// <summary>包含网关（条件并行，满足条件的全部执行）</summary>
    InclusiveGateway = 22,

    // ─── 子流程 ───
    /// <summary>子流程（嵌入式）</summary>
    SubProcess = 30,
    /// <summary>调用活动（引用外部流程定义）</summary>
    CallActivity = 31,

    // ─── 其他 ───
    /// <summary>抄送/知会节点</summary>
    CopyNode = 40,
    /// <summary>通知节点</summary>
    NotificationNode = 41,
    /// <summary>条件节点（旧版兼容）</summary>
    ConditionNode = 50
}
