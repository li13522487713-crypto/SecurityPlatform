namespace Atlas.Infrastructure.Channels.Options;

/// <summary>微信公众号 / 小程序 / 客服等多应用共享的开放平台配置片段（敏感信息走环境变量或密钥存储）。</summary>
public sealed class WeixinOpenChannelOptions
{
    public const string SectionName = "Atlas:Channels:Weixin";

    /// <summary>公众号 AppId（可选；未配置时 Senparc 注册跳过）。</summary>
    public string? MpAppId { get; set; }

    /// <summary>公众号 AppSecret。</summary>
    public string? MpAppSecret { get; set; }

    /// <summary>小程序 AppId。</summary>
    public string? WxOpenAppId { get; set; }

    /// <summary>小程序 AppSecret。</summary>
    public string? WxOpenAppSecret { get; set; }
}
