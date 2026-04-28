namespace Atlas.Domain.ExternalConnectors.Enums;

/// <summary>
/// 总报告 28.3 节描述的三种集成模式。每个 ApprovalFlowDefinition 的字段映射上挂一份。
/// </summary>
public enum IntegrationMode
{
    /// <summary>外部主导：表单提交直推外部审批中心，平台只采集表单与回流。</summary>
    ExternalLed = 1,

    /// <summary>本地主导：本平台审批引擎完整流转，外部只接收消息卡片。</summary>
    LocalLed = 2,

    /// <summary>双中心：本平台与外部同步流转，外部承接员工端待办入口。</summary>
    Hybrid = 3,
}
