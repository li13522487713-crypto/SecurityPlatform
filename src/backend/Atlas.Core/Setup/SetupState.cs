namespace Atlas.Core.Setup;

/// <summary>
/// 平台安装向导状态机：NotConfigured → Configuring → Migrating → Seeding → Ready
/// 任一中间状态失败可转入 Failed，Failed 可重试回到对应阶段。
/// </summary>
public enum SetupState
{
    NotConfigured = 0,
    Configuring = 1,
    Migrating = 2,
    Seeding = 3,
    Ready = 10,
    Failed = -1
}
