namespace Atlas.Infrastructure.Channels;

/// <summary>与 AI 数据库 <c>AiDatabaseChannelConfig</c> 对齐的渠道目录项（展示名 + 默认读写开关 + 凭据类型）。</summary>
public sealed record ChannelCatalogEntry(
    string ChannelKey,
    string DisplayName,
    string? PublishChannelType,
    string? CredentialKind,
    bool AllowDraft = true,
    bool AllowOnline = true);

/// <summary>全站渠道注册表：前端「渠道读写配置」与后端 bootstrap 共用同一顺序与 key。</summary>
public static class ChannelCatalog
{
    public static readonly IReadOnlyList<ChannelCatalogEntry> All =
    [
        new("api", "API", "open-api", null),
        new("chat-sdk", "Chat SDK", "web-sdk", null),
        new("web-sdk", "Web SDK", "web-sdk", null),
        new("coze-store", "扣子商店", null, null),
        new("template", "模板", null, null),
        new("feishu", "飞书", "feishu", "feishu"),
        new("wechat-miniapp", "微信小程序", "wechat-miniapp", "wechat-miniapp"),
        new("wechat-cs", "微信客服", "wechat-cs", "wechat-cs"),
        new("wechat-mp", "微信公众号", "wechat-mp", "wechat-mp")
    ];
}
