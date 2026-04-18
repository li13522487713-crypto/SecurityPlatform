namespace Atlas.Connectors.Feishu;

/// <summary>
/// 单个飞书 provider 实例的运行时配置（已解密）。
/// </summary>
public sealed record FeishuRuntimeOptions
{
    public required string AppId { get; init; }

    public required string AppSecret { get; init; }

    /// <summary>飞书租户 key，用于跨租户识别。</summary>
    public string? TenantKey { get; init; }

    public required string CallbackBaseUrl { get; init; }

    public IReadOnlyList<string> TrustedDomains { get; init; } = Array.Empty<string>();

    /// <summary>事件订阅 Verification Token（明文）。</summary>
    public string? EventVerificationToken { get; init; }

    /// <summary>事件订阅 Encrypt Key（base64 后 32 字节，AES-256）。</summary>
    public string? EventEncryptKey { get; init; }
}
