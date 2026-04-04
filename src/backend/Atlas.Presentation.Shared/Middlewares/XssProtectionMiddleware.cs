using Atlas.Application.Options;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace Atlas.Presentation.Shared.Middlewares;

/// <summary>
/// XSS 防护中间件：对 JSON Body 中的所有字符串字段和 QueryString 参数进行 HTML 转义净化。
/// 等保2.0 输入验证（8.1.3）合规实现。
/// </summary>
public sealed class XssProtectionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly XssOptions _options;
    private readonly ILogger<XssProtectionMiddleware> _logger;

    public XssProtectionMiddleware(
        RequestDelegate next,
        IOptions<XssOptions> options,
        ILogger<XssProtectionMiddleware> logger)
    {
        _next = next;
        _options = options.Value;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // 白名单路径跳过净化
        if (IsWhitelisted(path))
        {
            await _next(context);
            return;
        }

        // 净化 QueryString
        SanitizeQueryString(context);

        // 净化 JSON Body（仅对写方法的 application/json）
        if (IsJsonContentType(context.Request)
            && (HttpMethods.IsPost(context.Request.Method)
                || HttpMethods.IsPut(context.Request.Method)
                || HttpMethods.IsPatch(context.Request.Method)))
        {
            await SanitizeJsonBodyAsync(context);
        }

        await _next(context);
    }

    private bool IsWhitelisted(string path) =>
        _options.WhitelistPaths.Any(w =>
            path.StartsWith(w, StringComparison.OrdinalIgnoreCase));

    private static bool IsJsonContentType(HttpRequest request) =>
        request.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true;

    private static void SanitizeQueryString(HttpContext context)
    {
        if (!context.Request.Query.Any()) return;

        var sanitized = context.Request.Query
            .ToDictionary(
                kvp => kvp.Key,
                kvp => new Microsoft.Extensions.Primitives.StringValues(
                    kvp.Value.Select(v => v is null ? v : SanitizeString(v)).ToArray()
                ),
                StringComparer.OrdinalIgnoreCase
            );

        context.Request.Query = new Microsoft.AspNetCore.Http.QueryCollection(sanitized);
    }

    private async Task SanitizeJsonBodyAsync(HttpContext context)
    {
        context.Request.EnableBuffering();

        // 超过大小限制则跳过
        if (context.Request.ContentLength > _options.MaxBodySizeBytes)
        {
            _logger.LogDebug("XSS: Body size {Size} exceeds limit, skipping sanitization",
                context.Request.ContentLength);
            return;
        }

        try
        {
            var body = await new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true)
                .ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(body))
            {
                context.Request.Body.Position = 0;
                return;
            }

            using var doc = JsonDocument.Parse(body);
            var sanitizedJson = SanitizeJsonElement(doc.RootElement);
            var sanitizedBody = JsonSerializer.Serialize(sanitizedJson);

            var bytes = Encoding.UTF8.GetBytes(sanitizedBody);
            context.Request.Body = new MemoryStream(bytes);
            context.Request.ContentLength = bytes.Length;
        }
        catch (JsonException)
        {
            // 非有效 JSON，重置流后继续
            context.Request.Body.Position = 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "XSS sanitization failed, passing request through as-is");
            context.Request.Body.Position = 0;
        }
    }

    private static object? SanitizeJsonElement(JsonElement element, string? propertyName = null)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => SanitizeObject(element),
            JsonValueKind.Array => SanitizeArray(element),
            JsonValueKind.String => SanitizeStringValue(element.GetString() ?? string.Empty, propertyName),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l
                                  : element.TryGetDouble(out var d) ? d
                                  : (object?)element.GetRawText(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.GetRawText()
        };
    }

    private static Dictionary<string, object?> SanitizeObject(JsonElement element)
    {
        var result = new Dictionary<string, object?>();
        foreach (var property in element.EnumerateObject())
        {
            result[property.Name] = SanitizeJsonElement(property.Value, property.Name);
        }
        return result;
    }

    private static List<object?> SanitizeArray(JsonElement element)
    {
        var result = new List<object?>();
        foreach (var item in element.EnumerateArray())
        {
            result.Add(SanitizeJsonElement(item));
        }
        return result;
    }

    private static object SanitizeStringValue(string input, string? propertyName)
    {
        if (ShouldPreserveStructuredJson(propertyName)
            && TrySanitizeStructuredJson(input, out var sanitizedJson))
        {
            return sanitizedJson;
        }

        return SanitizeString(input);
    }

    private static bool ShouldPreserveStructuredJson(string? propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return false;
        }

        return propertyName.EndsWith("Json", StringComparison.OrdinalIgnoreCase)
            || string.Equals(propertyName, "aiConfig", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TrySanitizeStructuredJson(string input, out string sanitizedJson)
    {
        sanitizedJson = string.Empty;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(input);
            var sanitized = SanitizeJsonElement(doc.RootElement);
            sanitizedJson = JsonSerializer.Serialize(sanitized);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// 最小 HTML 转义：净化 XSS 常见注入字符，不破坏普通内容语义。
    /// </summary>
    private static string SanitizeString(string input) =>
        input
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#x27;");
}
