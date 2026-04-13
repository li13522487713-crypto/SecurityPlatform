namespace Atlas.Application.Options;

/// <summary>
/// XSS 防护中间件配置（等保2.0 输入验证要求）
/// </summary>
public sealed class XssOptions
{
    /// <summary>
    /// 跳过 XSS 净化的路径前缀列表（如富文本编辑器接口）
    /// </summary>
    public string[] WhitelistPaths { get; init; } = [];

    /// <summary>
    /// 允许处理的最大 Body 大小（字节），超过则跳过净化。默认 1 MB。
    /// </summary>
    public long MaxBodySizeBytes { get; init; } = 1_048_576;

    /// <summary>
    /// 需要按“结构化 JSON 字符串”保留语义的字段名。
    /// 这些字段会先解析成 JSON 后逐层净化，再序列化回 JSON 字符串，而不是整体做 HTML 转义。
    /// </summary>
    public string[] StructuredJsonPropertyNames { get; init; } = ["metadata", "aiConfig", "uiMetadata"];

    /// <summary>
    /// 需要按“结构化 JSON 字符串”保留语义的字段名后缀。
    /// </summary>
    public string[] StructuredJsonPropertySuffixes { get; init; } = ["Json"];
}
