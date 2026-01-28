namespace Atlas.Domain.Approval.Enums;

/// <summary>
/// 提醒类型
/// </summary>
public enum ReminderType
{
    /// <summary>节点超时提醒</summary>
    NodeTimeout = 1,

    /// <summary>变量级超时提醒</summary>
    VariableTimeout = 2,

    /// <summary>手动催办</summary>
    ManualReminder = 3
}
