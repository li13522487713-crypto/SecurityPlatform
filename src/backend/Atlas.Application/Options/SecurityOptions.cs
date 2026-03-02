namespace Atlas.Application.Options;

public sealed class SecurityOptions
{
    public bool EnforceHttps { get; init; } = true;

    /// <summary>
    /// Maximum concurrent active sessions per user. 0 = unlimited.
    /// When exceeded, the oldest session is revoked automatically.
    /// </summary>
    public int MaxConcurrentSessions { get; init; } = 5;

    /// <summary>
    /// 连续登录失败多少次后要求图形验证码（等保2.0防暴力破解）。0 = 禁用。
    /// </summary>
    public int CaptchaThreshold { get; init; } = 3;

    /// <summary>验证码有效期（秒）</summary>
    public int CaptchaExpirySeconds { get; init; } = 300;
}
