using System.Net;
using System.Text.Json;

namespace Atlas.Core.Utilities;

/// <summary>
/// 结构化 JSON 字符串辅助方法。
/// </summary>
public static class StructuredJsonStringUtility
{
    /// <summary>
    /// 尝试把原始 JSON 字符串或历史 HTML 编码后的 JSON 字符串归一化为可解析 JSON。
    /// </summary>
    public static bool TryNormalizeJsonString(string? input, out string normalizedJson)
    {
        normalizedJson = string.Empty;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        if (CanParseAsJson(input))
        {
            normalizedJson = input;
            return true;
        }

        var decoded = WebUtility.HtmlDecode(input);
        if (!string.Equals(decoded, input, StringComparison.Ordinal) && CanParseAsJson(decoded))
        {
            normalizedJson = decoded;
            return true;
        }

        return false;
    }

    private static bool CanParseAsJson(string input)
    {
        try
        {
            using var _ = JsonDocument.Parse(input);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
