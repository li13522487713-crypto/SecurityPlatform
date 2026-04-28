namespace Atlas.Domain.ExternalConnectors.Enums;

/// <summary>
/// 已支持的外部协同 provider 类型。新增 provider 必须同步：
/// 1. 在此枚举追加；
/// 2. 在 ConnectorRegistry.ProviderType 字符串与该枚举的 ToProviderType / Parse 中保持一致；
/// 3. 在 .slnx 注册对应的 Atlas.Connectors.Xxx 项目。
/// </summary>
public enum ConnectorProviderType
{
    Unknown = 0,
    WeCom = 1,
    Feishu = 2,
    DingTalk = 3,
    CustomOidc = 4,
}

public static class ConnectorProviderTypeExtensions
{
    public static string ToProviderType(this ConnectorProviderType value) => value switch
    {
        ConnectorProviderType.WeCom => "wecom",
        ConnectorProviderType.Feishu => "feishu",
        ConnectorProviderType.DingTalk => "dingtalk",
        ConnectorProviderType.CustomOidc => "custom_oidc",
        _ => "unknown",
    };

    public static ConnectorProviderType Parse(string providerType) => providerType?.ToLowerInvariant() switch
    {
        "wecom" => ConnectorProviderType.WeCom,
        "feishu" => ConnectorProviderType.Feishu,
        "dingtalk" => ConnectorProviderType.DingTalk,
        "custom_oidc" => ConnectorProviderType.CustomOidc,
        _ => ConnectorProviderType.Unknown,
    };
}
