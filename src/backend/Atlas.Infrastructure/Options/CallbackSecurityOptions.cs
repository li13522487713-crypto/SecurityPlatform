namespace Atlas.Infrastructure.Options;

/// <summary>
/// 回调安全配置选项
/// </summary>
public sealed class CallbackSecurityOptions
{
    /// <summary>
    /// 回调URL白名单（允许的回调域名/IP列表，为空则不限制）
    /// </summary>
    public IReadOnlyList<string> AllowedCallbackDomains { get; init; } = Array.Empty<string>();

    /// <summary>
    /// 数据保护主密钥（用于加密SecretKey）
    /// </summary>
    public string DataProtectionKey { get; init; } = string.Empty;
}
