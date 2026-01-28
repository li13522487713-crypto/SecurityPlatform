namespace Atlas.Application.Options;

/// <summary>
/// 审批模块种子数据配置选项
/// </summary>
public sealed class ApprovalSeedDataOptions
{
    /// <summary>是否启用种子数据初始化</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>是否初始化默认按钮配置</summary>
    public bool InitializeButtonConfigs { get; init; } = true;

    /// <summary>是否初始化示例流程定义（可选）</summary>
    public bool InitializeExampleFlows { get; init; } = false;
}
