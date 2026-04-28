namespace Atlas.Domain.ExternalConnectors.Enums;

/// <summary>
/// 外部协同平台类型。整数值锁定，新增 provider 仅追加，不能复用旧值。
/// </summary>
public enum ExternalProviderType
{
    Unknown = 0,
    WeCom = 1,
    Feishu = 2,
    DingTalk = 3,
    CustomOidc = 99,
}
