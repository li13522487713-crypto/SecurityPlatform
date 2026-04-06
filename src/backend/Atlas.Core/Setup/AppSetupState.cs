namespace Atlas.Core.Setup;

/// <summary>
/// 应用级安装状态：NotConfigured → Initializing → Ready
/// </summary>
public enum AppSetupState
{
    NotConfigured = 0,
    Initializing = 1,
    Ready = 10,
    Failed = -1
}
