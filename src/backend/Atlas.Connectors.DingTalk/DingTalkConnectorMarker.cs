namespace Atlas.Connectors.DingTalk;

/// <summary>
/// DingTalk 连接器占位标记。当前仅暴露 ProviderType 字符串，避免后续接入再改 .slnx 与
/// ConnectorRegistry 的 provider 枚举；具体身份/目录/审批/消息能力等待后续里程碑实现。
/// </summary>
public static class DingTalkConnectorMarker
{
    public const string ProviderType = "dingtalk";
}
